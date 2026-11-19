namespace Sim.System.FileManagement;

/// <summary>
/// Records resource access decisions for audit and compliance.
/// </summary>
public interface IResourceAccessAuditService
{
    /// <summary>
    /// Record a resource access attempt.
    /// </summary>
    Task RecordAccessAsync(
        string resourceType,
        Guid resourceId,
        Guid? actorUserId,
        bool granted,
        string accessReason,
        string? shareTokenHashPrefix = null,
        Guid? fileId = null,
        string? clientIp = null,
        string? userAgent = null,
        Guid? shareLinkId = null,
        string? visitorFingerprintHash = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query audit records for a specific resource.
    /// </summary>
    Task<IReadOnlyList<ResourceAccessAuditDto>> QueryByResourceAsync(
        string resourceType,
        Guid resourceId,
        int maxResults = 50,
        CancellationToken cancellationToken = default);
}
