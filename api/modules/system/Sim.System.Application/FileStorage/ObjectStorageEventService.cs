using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Sim.System.FileManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sim.Framework.Application.Contracts.RateLimiting;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Shared;
using Sim.Framework.Application.Services;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileStorage;

/// <summary>
/// Webhook endpoint for object storage event notifications (S3/COS/OSS download tracking).
/// Requests must carry a valid HMAC-SHA256 signature; anonymous unsigned writes are rejected.
/// </summary>
[AllowAnonymous]
public class ObjectStorageEventService(
    ISqlSugarRepository<FileDownloadRecordEntity> downloadRepository,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ObjectStorageEventService> logger) : ApplicationService, IObjectStorageEventService
{
    private const string SignatureHeader = "X-Sim-Signature";
    private const int MaxEventBodyLength = 1_000_000;

    /// <summary>
    /// 接收对象存储事件通知（S3/COS/OSS），校验 HMAC 签名后记录文件下载事件。
    /// </summary>
    [RateLimit(permitLimit: 60, windowSeconds: 60, PartitionName = "object-storage-webhook")]
    public async Task ReceiveS3EventAsync(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken = default)
    {
        var rawBody = body.ValueKind == JsonValueKind.Undefined ? string.Empty : body.GetRawText();
        VerifySignature(rawBody);
        if (string.IsNullOrWhiteSpace(rawBody) || body.ValueKind != JsonValueKind.Object)
        {
            logger.LogWarning("Empty or non-object object storage event body received");
            return;
        }

        if (rawBody.Length > MaxEventBodyLength)
        {
            throw new BusinessException(
                SystemErrorCodes.InvalidFileSize,
                "The object storage event body is too large.");
        }

        try
        {
            // AWS S3 Event Notification format: { "Records": [...] }
            if (body.TryGetProperty("Records", out var records) && records.ValueKind == JsonValueKind.Array)
            {
                await ProcessEvents(records, cancellationToken);
                return;
            }

            logger.LogWarning("Unrecognized object storage event format: {Body}",
                rawBody[..Math.Min(rawBody.Length, 500)]);
        }
        catch (Exception ex)
        {
            // Always swallow — prevent object storage from retrying
            logger.LogError(ex, "Failed to process object storage event");
        }
    }

    private void VerifySignature(string? body)
    {
        var secret = configuration["FileManagement:ObjectStorageWebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new AuthorizationException(
                SystemErrorCodes.ObjectStorageWebhookNotConfigured,
                "The object storage webhook signing secret is not configured.");
        }

        var provided = httpContextAccessor.HttpContext?.Request.Headers[SignatureHeader].ToString();
        if (string.IsNullOrWhiteSpace(provided))
        {
            throw InvalidSignature();
        }

        var normalized = provided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? provided["sha256=".Length..]
            : provided;
        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(normalized);
        }
        catch (FormatException)
        {
            try
            {
                providedBytes = Convert.FromBase64String(normalized);
            }
            catch (FormatException)
            {
                throw InvalidSignature();
            }
        }

        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(body ?? string.Empty));
        if (providedBytes.Length != expected.Length ||
            !CryptographicOperations.FixedTimeEquals(providedBytes, expected))
        {
            throw InvalidSignature();
        }
    }

    private static AuthorizationException InvalidSignature() =>
        new(
            SystemErrorCodes.ObjectStorageWebhookSignatureInvalid,
            "The object storage webhook signature is invalid.");

    private async Task ProcessEvents(
        JsonElement records,
        CancellationToken cancellationToken)
    {
        foreach (var record in records.EnumerateArray())
        {
            try
            {
                var eventName = record.TryGetProperty("eventName", out var ev) ? ev.GetString() : null;
                if (eventName is null or "")
                {
                    eventName = record.TryGetProperty("Event", out var ev2) ? ev2.GetString() : "Unknown";
                }

                // Only track ObjectAccessed / GetObject events
                if (eventName is null
                    || (!eventName.Contains("ObjectAccessed", StringComparison.OrdinalIgnoreCase)
                        && !eventName.Contains("Get", StringComparison.OrdinalIgnoreCase)
                        && !eventName.Contains("Download", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var s3 = record.TryGetProperty("s3", out var s3Prop) ? s3Prop : default;
                var obj = s3.ValueKind != JsonValueKind.Undefined
                    ? (s3.TryGetProperty("object", out var o) ? o : default)
                    : default;
                var key = obj.ValueKind != JsonValueKind.Undefined
                    ? (obj.TryGetProperty("key", out var k) ? k.GetString() : null)
                    : null;

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                // Decode URL-encoded key
                key = Uri.UnescapeDataString(key);

                var requestParams = record.TryGetProperty("requestParameters", out var rp)
                    ? rp : default;
                var sourceIp = requestParams.ValueKind != JsonValueKind.Undefined
                    ? (requestParams.TryGetProperty("sourceIPAddress", out var sip) ? sip.GetString() : null)
                    : null;

                var downloadRecord = new FileDownloadRecordEntity(Guid.NewGuid())
                {
                    Provider = "s3",
                    StorageKey = key,
                    StartedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    ClientIp = sourceIp,
                    Status = DownloadStatus.Completed
                };

                await downloadRepository.InsertAsync(downloadRecord);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process individual S3 event record");
            }
        }
    }
}
