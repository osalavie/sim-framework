using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sim.Framework.Application.Content;
using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.FileManagement;
using Sim.System.Application.Internal;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared;
using Sim.Framework.Application.Services;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileManagement;

/// <summary>
/// HMAC-signed file download service for Local/Database storage providers.
/// Validates the access token and streams the file with download tracking.
/// </summary>
[AllowAnonymous]
public class FileAccessService(
    IFileStorage fileStorage,
    IOptions<FileManagementOptions> options,
    ISqlSugarRepository<FileDownloadRecordEntity> downloadRepository,
    ILogger<FileAccessService> logger) : ApplicationService, IFileAccessService
{
    /// <summary>
    /// 使用 HMAC 签名令牌下载 Local/Database 存储提供方的文件。
    /// </summary>
    public async Task<IRemoteStreamContent> DownloadAsync(
        string provider,
        string key,
        string token,
        long expires,
        CancellationToken cancellationToken = default)
    {
        var fileOptions = options.Value;

        // ── Validate HMAC Token ──────────────────────────────
        if (string.IsNullOrWhiteSpace(token) || expires <= 0)
        {
            throw new BusinessException(SystemErrorCodes.InvalidAccessToken)
                .WithData("Reason", "Missing or invalid token/expires parameters.");
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expires);
        if (DateTimeOffset.UtcNow >= expiresAt)
        {
            throw new BusinessException(SystemErrorCodes.TokenExpired)
                .WithData("Reason", "Access URL has expired.");
        }

        if (string.IsNullOrWhiteSpace(fileOptions.AccessUrlSigningKey))
        {
            throw new BusinessException(SystemErrorCodes.HmacNotConfigured)
                .WithData("Reason", "Server not configured for HMAC file access.");
        }

        if (!FileAccessTokenHelper.Validate(key, expires, token, fileOptions.AccessUrlSigningKey))
        {
            logger.LogWarning("Invalid file access token for provider={Provider} key={Key}", provider, key);
            throw new BusinessException(SystemErrorCodes.InvalidAccessToken)
                .WithData("Reason", "Invalid access token.");
        }

        // ── Only Local/Database providers use HMAC ───────────
        if (provider is not (FileStorageProviderNames.Local or FileStorageProviderNames.Database))
        {
            throw new BusinessException(SystemErrorCodes.UnsupportedProvider)
                .WithData("Reason", $"Provider '{provider}' does not support HMAC access URLs.");
        }

        // ── Read File ────────────────────────────────────────
        var result = await fileStorage.OpenReadAsync(key, provider, cancellationToken);
        if (result == null)
        {
            throw new BusinessException(SystemErrorCodes.FileNotFound);
        }

        // ── Create Download Record ───────────────────────────
        var record = new FileDownloadRecordEntity(Guid.NewGuid())
        {
            Provider = provider,
            StorageKey = key,
            StartedAt = DateTime.UtcNow,
            Status = DownloadStatus.Started
        };
        await downloadRepository.InsertAsync(record);

        // ── Wrap Stream with Download Tracking ───────────────
        var trackingStream = new DownloadTrackingStream(
            result.Content,
            async (bytesWritten) =>
            {
                try
                {
                    record.BytesTransferred = bytesWritten;
                    record.CompletedAt = DateTime.UtcNow;
                    record.Status = DownloadStatus.Completed;
                    await downloadRepository.UpdateAsync(record);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to update download record {RecordId}", record.Id);
                }
            },
            async (ex) =>
            {
                try
                {
                    record.Status = DownloadStatus.Failed;
                    record.CompletedAt = DateTime.UtcNow;
                    record.Error = ex.Message[..Math.Min(ex.Message.Length, 1024)];
                    await downloadRepository.UpdateAsync(record);
                }
                catch (Exception innerEx)
                {
                    logger.LogWarning(innerEx, "Failed to update failed download record {RecordId}", record.Id);
                }
            });

        return new RemoteStreamContent(trackingStream, result.FileName, result.ContentType, result.Length);
    }
}
