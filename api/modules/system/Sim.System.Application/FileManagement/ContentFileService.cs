using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sim.Framework.Application.Content;
using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileManagement;

public sealed class ContentFileService(
    IFileStorage storage,
    IFileManagementOptionsProvider optionsProvider,
    IFileValidator fileValidator,
    IQuarantineService quarantineService,
    ISqlSugarRepository<FileMetadataEntity> metadataRepository,
    IResourceAccessService resourceAccessService,
    IHttpContextAccessor httpContextAccessor) : ApplicationService, IContentFileService
{
    /// <summary>
    /// 上传内容文件（支持客户端 E2EE 加密文件），返回文件元数据与访问地址。
    /// </summary>
    [Authorize]
    [RateLimit(permitLimit: 30, windowSeconds: 60, PartitionName = "content-file-upload")]
    public async Task<ContentFileDto> UploadAsync(
        IRemoteStreamContent file,
        CancellationToken cancellationToken = default)
    {
        var ownerId = CurrentUser.Id ?? throw new UnauthorizedAccessException();
        using (file)
        {
            var options = await optionsProvider.GetAsync(cancellationToken);
            var fileName = NormalizeFileName(file.FileName);
            BufferedFile buffered;
            try
            {
                buffered = await FileStreamUtilities.BufferAsync(
                    file.GetStream(),
                    options.MaxUploadBytes,
                    cancellationToken);
            }
            catch (FileSizeLimitExceededException)
            {
                throw new BusinessException(SystemErrorCodes.FileSizeExceeded)
                    .WithData("MaxSizeMb", Math.Round(options.MaxUploadBytes / 1048576d, 1));
            }
            await using var content = buffered.Content;
            var encrypted = fileName.EndsWith(".sime2ee", StringComparison.OrdinalIgnoreCase);
            string extension;
            string contentType;
            if (encrypted)
            {
                if (buffered.Length < 42 || buffered.Length > 20 * 1024 * 1024 + 8192 ||
                    file.ContentType != "application/vnd.sim.e2ee")
                    throw new BusinessException(SystemErrorCodes.InvalidFileSize, "Encrypted file size or media type is invalid.");
                var magic = new byte[8];
                await content.ReadExactlyAsync(magic, cancellationToken);
                if (!magic.AsSpan().SequenceEqual("SIME2EE1"u8))
                    throw new BusinessException(SystemErrorCodes.InvalidFileName, "Encrypted file header is invalid.");
                extension = ".sime2ee";
                contentType = "application/vnd.sim.e2ee";
            }
            else
            {
                (extension, contentType) = fileValidator.Validate(fileName, content, file.ContentType);
            }
            content.Position = 0;

            var key = $"content/{ownerId:N}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
            var stored = await storage.SaveAsync(
                new FileStorageWriteRequest(
                    key,
                    fileName,
                    contentType,
                    content,
                    buffered.Length,
                    buffered.Sha256),
                cancellationToken: cancellationToken);

            var metadata = new FileMetadataEntity
            {
                CreatorId = ownerId,
                StorageKey = stored.Key,
                Provider = stored.Provider,
                FileName = stored.FileName,
                ContentType = stored.ContentType,
                Length = stored.Length,
                Sha256 = stored.Sha256,
                ScanStatus = QuarantineStatus.Pending,
                ScannerName = encrypted ? "client-e2ee-unscannable" : null
            };
            await metadataRepository.InsertAsync(metadata);

            if (!encrypted) _ = quarantineService.RegisterAsync(stored.Key, stored.Provider, CancellationToken.None)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        Logger.LogWarning(
                            task.Exception,
                            "Failed to register content file {Key} for quarantine scan",
                            stored.Key);
                    }
                }, TaskScheduler.Default);

            return new ContentFileDto
            {
                Id = metadata.Id,
                Provider = stored.Provider,
                Key = stored.Key,
                FileName = stored.FileName,
                ContentType = stored.ContentType,
                Length = stored.Length,
                Url = $"/api/system/content-file/{metadata.Id:D}"
            };
        }
    }

    /// <summary>
    /// 按文件 ID 读取内容文件，支持分享令牌访问与 ETag 协商缓存。
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [RateLimit]
    public async Task<IRemoteStreamContent> GetAsync(
        Guid id,
        string? shareToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!await resourceAccessService.CanAccessFileAsync(
                id,
                CurrentUser.Id,
                shareToken,
                CreateAccessContext(),
                cancellationToken))
        {
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        var metadata = await metadataRepository.GetAsync(id);
        if (metadata == null)
        {
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        var httpContext = httpContextAccessor.HttpContext;
        // 内容文件按 ID 内容不可变：稳定 ETag 命中 If-None-Match 直接 304，免去打开存储与传输
        var etag = string.IsNullOrWhiteSpace(metadata.Sha256) ? null : $"\"{metadata.Sha256}\"";
        if (httpContext != null && etag != null && MatchesIfNoneMatch(httpContext.Request.Headers.IfNoneMatch, etag))
        {
            httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
            return new RemoteStreamContent(Stream.Null, null, metadata.ContentType, null, disposeStream: false);
        }

        var stored = await storage.OpenReadAsync(
            metadata.StorageKey,
            metadata.Provider,
            cancellationToken);
        if (stored == null)
        {
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        if (httpContext != null)
        {
            // private：只允许浏览器缓存，CDN/共享缓存不得缓存带鉴权的内容；
            // 带 Authorization 的请求浏览器不直接复用，由前端 getBlob 会话缓存与并发去重兜底
            httpContext.Response.Headers.CacheControl = "private, max-age=86400";
            if (etag != null)
            {
                httpContext.Response.Headers.ETag = etag;
            }
        }

        return new RemoteStreamContent(
            stored.Content,
            stored.FileName,
            stored.ContentType,
            stored.Length);
    }

    private static bool MatchesIfNoneMatch(string? headerValue, string etag)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        foreach (var raw in headerValue.Split(','))
        {
            var candidate = raw.Trim();
            if (candidate == "*")
            {
                return true;
            }

            if (candidate.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate[2..];
            }

            if (candidate.Equals(etag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private ResourceAccessContext CreateAccessContext()
    {
        var context = httpContextAccessor.HttpContext;
        var remoteIp = context?.Connection.RemoteIpAddress;
        remoteIp = remoteIp is { IsIPv4MappedToIPv6: true } ? remoteIp.MapToIPv4() : remoteIp;
        return new ResourceAccessContext(
            context?.Request.Headers["X-Visitor-Fingerprint"].ToString().Trim().ToLowerInvariant(),
            remoteIp?.ToString(),
            context?.Request.Headers.UserAgent.ToString());
    }

    private static string NormalizeFileName(string? fileName)
    {
        var normalized = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(normalized) ? "file.bin" : normalized;
    }
}
