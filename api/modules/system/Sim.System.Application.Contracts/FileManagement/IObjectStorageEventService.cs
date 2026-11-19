using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.FileManagement;

/// <summary>
/// Webhook endpoint for object storage event notifications (S3/COS/OSS download tracking).
///
/// Configure your object storage bucket to POST events to:
///   POST /api/system/object-storage-event/receive-s3-event
///
/// Supported event formats:
/// - AWS S3 Event Notifications
/// - Tencent COS Callback (simplified JSON)
/// </summary>
public interface IObjectStorageEventService : IApplicationService
{
    /// <summary>
    /// Receive S3-compatible event notifications for download tracking.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("receive-s3-event")]
    Task ReceiveS3EventAsync(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken = default);
}
