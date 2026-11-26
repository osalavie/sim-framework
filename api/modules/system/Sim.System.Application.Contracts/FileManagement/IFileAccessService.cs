using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.FileManagement;

/// <summary>
/// HMAC-signed file download service for Local/Database storage providers.
/// Validates the access token and streams the file directly to the client.
/// Downloads are tracked in FileDownloadRecordEntity for monitoring.
///
/// S3/COS/OSS providers use native pre-signed URLs and do NOT go through this endpoint.
/// Their downloads are tracked via object storage event notification webhooks.
/// </summary>
public interface IFileAccessService : IApplicationService
{
    /// <summary>
    /// Download a file using an HMAC-signed access URL (Local/Database providers only).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{provider}/{**key}")]
    Task<IRemoteStreamContent> DownloadAsync(
        string provider,
        string key,
        string token,
        long expires,
        CancellationToken cancellationToken = default);
}
