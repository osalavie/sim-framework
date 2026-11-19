using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Sim.Framework.Application.Content;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared;
using Sim.Framework.Application.Services;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileManagement;

[Authorize]
[Permission(FileManagementPermissions.Manage)]
public class FileManagementService(
    IFileStorage storage,
    IFileTemplateRenderer templateRenderer,
    IFileExportService exportService,
    IFileManagementOptionsProvider optionsProvider,
    IFileValidator fileValidator,
    IHttpContextAccessor httpContextAccessor,
    IQuarantineService quarantineService,
    IConversionQueue conversionQueue,
    ISqlSugarRepository<FileMetadataEntity> metadataRepository,
    IChunkedUploadService chunkedUploadService) : ApplicationService, IFileManagementService
{
    /// <summary>
    /// 上传文件到指定存储提供方，完成内容校验、隔离扫描登记与元数据记录。
    /// </summary>
    [RateLimit(permitLimit: 30, windowSeconds: 60, PartitionName = "file-storage-write")]
    public async Task<StoredFileDto> UploadAsync(
        IRemoteStreamContent file,
        string? key = null,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
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
            await using var bufferedContent = buffered.Content;

            // Validate file via magic-number detection + MIME cross-validation + extension whitelist
            var (validatedExtension, validatedMimeType) = fileValidator.Validate(
                fileName,
                bufferedContent,
                file.ContentType);
            bufferedContent.Position = 0;

            // Use validated extension for key generation (overrides client-supplied extension)
            key = string.IsNullOrWhiteSpace(key)
                ? $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{validatedExtension}"
                : key;
            var result = await storage.SaveAsync(
                new FileStorageWriteRequest(
                    key,
                    fileName,
                    validatedMimeType,
                    bufferedContent,
                    buffered.Length,
                    buffered.Sha256),
                provider,
                cancellationToken);

            // Register for quarantine scan (non-blocking — scan runs in background)
            _ = quarantineService.RegisterAsync(result.Key, result.Provider, CancellationToken.None)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Logger.Log(LogLevel.Warning, t.Exception,
                            "Failed to register file {Key} for quarantine scan", result.Key);
                }, TaskScheduler.Default);

            // Persist file metadata for audit, search, and lifecycle management
            var metadata = new FileMetadataEntity
            {
                StorageKey = result.Key,
                Provider = result.Provider,
                FileName = result.FileName,
                ContentType = result.ContentType,
                Length = result.Length,
                Sha256 = result.Sha256,
                ScanStatus = QuarantineStatus.Pending
            };
            await metadataRepository.InsertAsync(metadata);

            return Map(result);
        }
    }

    /// <summary>
    /// 下载指定文件；已判定为感染的文件禁止下载。
    /// </summary>
    public async Task<IRemoteStreamContent> GetDownloadAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default)
    {
        // Download gate: block downloads of infected files
        var metadata = await metadataRepository.GetQueryable()
            .FirstAsync(entity => entity.StorageKey == key, cancellationToken);
        if (metadata != null && metadata.ScanStatus == QuarantineStatus.Infected)
        {
            Logger.LogWarning("Blocked download of infected file {Key} (scanner: {Scanner}, result: {Result})",
                key, metadata.ScannerName, metadata.ScanResult);
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        var resolvedProvider = await ResolveProviderAsync(key, provider, cancellationToken);
        var result = await storage.OpenReadAsync(key, resolvedProvider, cancellationToken);
        if (result == null)
        {
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        SetDownloadHeaders(result.FileName, result.ContentType);
        return ToRemoteContent(result.Content, result.FileName, result.ContentType, result.Length);
    }

    /// <summary>
    /// 获取指定文件的带签名访问地址。
    /// </summary>
    public async Task<FileAccessUrlDto> GetDownloadUrlAsync(
        string key,
        string? provider = null,
        TimeSpan? expiresIn = null,
        CancellationToken cancellationToken = default)
    {
        // Download gate: block presigned URLs for infected files
        var metadata = await metadataRepository.GetQueryable()
            .FirstAsync(entity => entity.StorageKey == key, cancellationToken);
        if (metadata != null && metadata.ScanStatus == QuarantineStatus.Infected)
        {
            Logger.LogWarning("Blocked presigned URL for infected file {Key}", key);
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        var resolvedProvider = await ResolveProviderAsync(key, provider, cancellationToken);
        var effectiveExpiresIn = expiresIn ?? TimeSpan.FromMinutes(30);
        var result = await storage.GenerateAccessUrlAsync(key, effectiveExpiresIn, resolvedProvider, cancellationToken);
        if (result == null)
        {
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        return new FileAccessUrlDto
        {
            Provider = result.Provider,
            Key = result.Key,
            Url = result.Url,
            ExpiresAt = result.ExpiresAt,
            IsExternalUrl = result.IsExternalUrl
        };
    }

    /// <summary>
    /// Resolves the correct storage provider for a file download.
    /// If the caller explicitly passes a provider, use it.
    /// Otherwise, look up the file's metadata to find the provider where it was stored,
    /// falling back to the system default only when no metadata exists (legacy files).
    /// This ensures files remain downloadable after the default storage provider is changed.
    /// </summary>
    private async Task<string?> ResolveProviderAsync(
        string key,
        string? explicitProvider,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(explicitProvider))
        {
            return explicitProvider;
        }

        var metadata = await metadataRepository.GetQueryable()
            .FirstAsync(entity => entity.StorageKey == key, cancellationToken);

        return metadata?.Provider;
    }

    private void SetDownloadHeaders(string fileName, string contentType)
    {
        var context = httpContextAccessor.HttpContext;
        if (context == null) return;

        // X-Content-Type-Options: nosniff — prevents MIME sniffing
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // For non-image/non-audio/non-video files, force download (attachment)
        // to prevent active content from executing in the browser
        var isInlineSafe = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                           contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ||
                           contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ||
                           contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

        var encodedFileName = HttpUtility.UrlEncode(fileName, Encoding.UTF8);
        var disposition = isInlineSafe
            ? $"inline; filename=\"{encodedFileName}\""
            : $"attachment; filename=\"{encodedFileName}\"";

        context.Response.Headers["Content-Disposition"] = disposition;
    }

    /// <summary>
    /// 删除指定文件。
    /// </summary>
    public Task DeleteAsync(
        string key,
        string? provider = null,
        CancellationToken cancellationToken = default) =>
        storage.DeleteAsync(key, provider, cancellationToken);

    /// <summary>
    /// 使用模板与 JSON 模型渲染并返回文本文件。
    /// </summary>
    [RateLimit(permitLimit: 20, windowSeconds: 60, PartitionName = "file-template-render")]
    public async Task<IRemoteStreamContent> RenderTemplateAsync(
        TemplateRenderInput input,
        CancellationToken cancellationToken = default)
    {
        var model = ParseModel(input.ModelJson);
        var rendered = await templateRenderer.RenderAsync(
            input.Template,
            model,
            cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(rendered);
        return ToRemoteContent(
            new MemoryStream(bytes, writable: false),
            NormalizeFileName(input.FileName),
            NormalizeContentType(input.ContentType),
            bytes.LongLength);
    }

    /// <summary>
    /// 将表格数据导出为指定格式的文件。
    /// </summary>
    public async Task<IRemoteStreamContent> ExportAsync(
        TabularExportInput input,
        CancellationToken cancellationToken = default)
    {
        var result = await exportService.ExportAsync(
            new TabularExportRequest(
                input.FileName,
                input.Columns,
                input.Rows,
                (TabularExportFormat)input.Format),
            cancellationToken);
        return ToRemoteContent(result.Content, result.FileName, result.ContentType, result.Length);
    }

    /// <summary>
    /// 提交文档转 PDF 任务，返回包含任务 ID 的处理状态。
    /// </summary>
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "file-office-conversion")]
    public async Task<IRemoteStreamContent> ConvertToPdfAsync(
        IRemoteStreamContent file,
        CancellationToken cancellationToken = default)
    {
        using (file)
        {
            var buffer = new byte[file.GetStream().Length > 0
                ? (int)Math.Min(file.GetStream().Length, int.MaxValue)
                : 0];
            if (buffer.Length > 0)
            {
                var read = await file.GetStream().ReadAsync(buffer, cancellationToken);
                if (read < buffer.Length)
                    Array.Resize(ref buffer, read);
            }

            var task = new ConversionTask
            {
                Id = Guid.NewGuid(),
                TaskType = ConversionTaskType.DocumentToPdf,
                InputFileName = NormalizeFileName(file.FileName),
                InputContent = buffer
            };

            await conversionQueue.EnqueueAsync(task, cancellationToken);
            var statusJson = Encoding.UTF8.GetBytes(
                $"{{\"status\":\"processing\",\"taskId\":\"{task.Id}\"}}");
            return ToRemoteContent(
                new MemoryStream(statusJson, writable: false),
                "conversion-status.json",
                "application/json",
                statusJson.LongLength);
        }
    }

    /// <summary>
    /// 提交图片处理任务（裁剪、缩放、压缩），返回包含任务 ID 的处理状态。
    /// </summary>
    [RateLimit(permitLimit: 20, windowSeconds: 60, PartitionName = "file-image-processing")]
    public async Task<IRemoteStreamContent> ProcessImageAsync(
        IRemoteStreamContent file,
        int? cropX = null,
        int? cropY = null,
        int? cropWidth = null,
        int? cropHeight = null,
        int? maxWidth = null,
        int? maxHeight = null,
        int quality = 82,
        string? outputExtension = null,
        CancellationToken cancellationToken = default)
    {
        using (file)
        {
            var buffer = new byte[file.GetStream().Length > 0
                ? (int)Math.Min(file.GetStream().Length, int.MaxValue)
                : 0];
            if (buffer.Length > 0)
            {
                var read = await file.GetStream().ReadAsync(buffer, cancellationToken);
                if (read < buffer.Length)
                    Array.Resize(ref buffer, read);
            }

            var task = new ConversionTask
            {
                Id = Guid.NewGuid(),
                TaskType = ConversionTaskType.ImageProcessing,
                InputFileName = NormalizeFileName(file.FileName),
                InputContent = buffer,
                Quality = quality,
                OutputExtension = outputExtension
            };

            await conversionQueue.EnqueueAsync(task, cancellationToken);
            var statusJson = Encoding.UTF8.GetBytes(
                $"{{\"status\":\"processing\",\"taskId\":\"{task.Id}\"}}");
            return ToRemoteContent(
                new MemoryStream(statusJson, writable: false),
                "conversion-status.json",
                "application/json",
                statusJson.LongLength);
        }
    }

    /// <summary>
    /// Gets the status of a previously submitted conversion task.
    /// </summary>
    public async Task<ConversionStatusDto?> GetConversionStatusAsync(Guid taskId)
    {
        var task = await conversionQueue.GetStatusAsync(taskId);
        if (task == null) return null;
        return new ConversionStatusDto
        {
            TaskId = task.Id,
            TaskType = task.TaskType,
            Status = task.Status,
            InputFileName = task.InputFileName,
            ResultFileName = task.ResultFileName,
            Error = task.Error,
            RetryCount = task.RetryCount,
            CreatedAt = task.CreatedAt,
            CompletedAt = task.CompletedAt
        };
    }

    /// <summary>
    /// Downloads the result of a completed conversion task.
    /// </summary>
    public async Task<IRemoteStreamContent> GetConversionResultAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var result = await conversionQueue.GetResultAsync(taskId, cancellationToken);
        if (result == null)
        {
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }
        SetDownloadHeaders(result.FileName, result.ContentType);
        return ToRemoteContent(result.Content, result.FileName, result.ContentType, result.Length);
    }

    private static StoredFileDto Map(FileStorageWriteResult result) => new()
    {
        Provider = result.Provider,
        Key = result.Key,
        FileName = result.FileName,
        ContentType = result.ContentType,
        Length = result.Length,
        Sha256 = result.Sha256
    };

    private static IRemoteStreamContent ToRemoteContent(
        Stream content,
        string fileName,
        string contentType,
        long length) =>
        new RemoteStreamContent(content, fileName, contentType, length);

    private static string NormalizeFileName(string? fileName)
    {
        var normalized = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(normalized) ? "file.bin" : normalized;
    }

    private static string NormalizeContentType(string? contentType) =>
        string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;

    private static IReadOnlyDictionary<string, object?> ParseModel(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Template model must be a JSON object.");
            }

            return (IReadOnlyDictionary<string, object?>)ConvertElement(document.RootElement)!;
        }
        catch (JsonException exception)
        {
            throw new BusinessException(SystemErrorCodes.InvalidTemplateModel)
                .WithData("Reason", exception.Message);
        }
    }

    private static object? ConvertElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => ConvertElement(property.Value),
            StringComparer.Ordinal),
        JsonValueKind.Array => element.EnumerateArray().Select(ConvertElement).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number => element.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText()
    };

    // ── Chunked Upload ──────────────────────────────────

    /// <summary>
    /// 初始化分片上传会话。
    /// </summary>
    [RateLimit(permitLimit: 10, windowSeconds: 60, PartitionName = "file-chunked-upload")]
    public Task<ChunkUploadSessionDto> ChunkUploadInitAsync(
        ChunkUploadInitRequest input, CancellationToken cancellationToken = default) =>
        chunkedUploadService.InitSessionAsync(input, cancellationToken);

    /// <summary>
    /// 上传指定分片并返回上传进度。
    /// </summary>
    [RateLimit(permitLimit: 60, windowSeconds: 60, PartitionName = "file-chunked-upload")]
    public async Task<ChunkUploadProgressDto> ChunkUploadChunkAsync(
        Guid sessionId, int chunkIndex, IRemoteStreamContent chunk, CancellationToken cancellationToken = default)
    {
        using (chunk)
        {
            return await chunkedUploadService.UploadChunkAsync(
                new ChunkUploadChunkRequest
                {
                    SessionId = sessionId,
                    ChunkIndex = chunkIndex,
                    Content = chunk.GetStream()
                }, cancellationToken);
        }
    }

    /// <summary>
    /// 完成分片上传并合并文件。
    /// </summary>
    public async Task<ChunkUploadCompletionDto> ChunkUploadCompleteAsync(
        Guid sessionId, string? provider = null, CancellationToken cancellationToken = default) =>
        await chunkedUploadService.CompleteUploadAsync(sessionId, provider, cancellationToken);

    /// <summary>
    /// 中止分片上传会话。
    /// </summary>
    public async Task ChunkUploadAbortAsync(
        Guid sessionId, CancellationToken cancellationToken = default) =>
        await chunkedUploadService.AbortUploadAsync(sessionId, cancellationToken);
}
