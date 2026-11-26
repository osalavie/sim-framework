using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileManagement;

public sealed class ResourceAccessAuditService(
    ISqlSugarRepository<ResourceAccessAuditEntity> auditRepository) : IResourceAccessAuditService
{
    public async Task RecordAccessAsync(
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
        CancellationToken cancellationToken = default)
    {
        var entity = new ResourceAccessAuditEntity
        {
            Id = Guid.NewGuid(),
            ResourceType = resourceType,
            ResourceId = resourceId,
            ActorUserId = actorUserId,
            Decision = granted ? "Granted" : "Denied",
            AccessReason = accessReason,
            ShareTokenHashPrefix = shareTokenHashPrefix?.Length > 64
                ? shareTokenHashPrefix[..64]
                : shareTokenHashPrefix,
            VisitorFingerprintHash = visitorFingerprintHash?.Length > 64
                ? visitorFingerprintHash[..64]
                : visitorFingerprintHash,
            ShareLinkId = shareLinkId,
            FileId = fileId,
            AccessedAt = DateTime.UtcNow,
            ClientIp = clientIp?.Length > 45 ? clientIp[..45] : clientIp,
            UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent
        };
        await auditRepository.InsertAsync(entity);
    }

    public async Task<IReadOnlyList<ResourceAccessAuditDto>> QueryByResourceAsync(
        string resourceType,
        Guid resourceId,
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        var records = await auditRepository.GetQueryable()
            .Where(a => a.ResourceType == resourceType && a.ResourceId == resourceId)
            .OrderBy(a => a.AccessedAt, OrderByType.Desc)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return records.Select(a => new ResourceAccessAuditDto
        {
            Id = a.Id,
            ResourceType = a.ResourceType,
            ResourceId = a.ResourceId,
            ActorUserId = a.ActorUserId,
            Decision = a.Decision,
            AccessReason = a.AccessReason,
            ShareLinkId = a.ShareLinkId,
            VisitorFingerprintHash = a.VisitorFingerprintHash,
            FileId = a.FileId,
            AccessedAt = a.AccessedAt,
            ClientIp = a.ClientIp,
            UserAgent = a.UserAgent
        }).ToList();
    }
}
