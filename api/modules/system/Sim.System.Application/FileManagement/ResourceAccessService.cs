using System.Security.Cryptography;
using System.Text;
using Sim.System.Domain.Shared;
using Sim.System.FileManagement;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.Shared.Enums;
using Sim.System.Domain.FileManagement;

namespace Sim.System.Application.FileManagement;

public sealed class ResourceAccessService(
    ISqlSugarRepository<ResourceAccessPolicyEntity> policyRepository,
    ISqlSugarRepository<ResourceFileReferenceEntity> referenceRepository,
    ISqlSugarRepository<ResourceShareLinkEntity> shareRepository,
    ISqlSugarRepository<ResourceShareLinkViewerEntity> shareViewerRepository,
    ISqlSugarRepository<FileMetadataEntity> metadataRepository,
    IResourceAccessAuditService auditService) : IResourceAccessService
{
    private const int CurrentShareTokenVersion = 1;

    public async Task EnsureEncryptedFilesAsync(Guid ownerId, IReadOnlyCollection<Guid> fileIds)
    {
        var ids = fileIds.Distinct().ToArray();
        if (ids.Length == 0) return;
        var files = await metadataRepository.GetListAsync(file => ids.Contains(file.Id));
        if (files.Count != ids.Length || files.Any(file => file.CreatorId != ownerId || file.ContentType != "application/vnd.sim.e2ee"))
            throw new UnauthorizedAccessException("Encrypted resources require owned encrypted files.");
    }

    public async Task ConfigureAsync(
        string resourceType,
        Guid resourceId,
        Guid ownerId,
        ResourceVisibility visibility,
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken cancellationToken = default)
    {
        resourceType = NormalizeResourceType(resourceType);
        var policy = await policyRepository.GetQueryable()
            .FirstAsync(x => x.ResourceType == resourceType && x.ResourceId == resourceId, cancellationToken);

        if (policy == null)
        {
            policy = new ResourceAccessPolicyEntity
            {
                ResourceType = resourceType,
                ResourceId = resourceId,
                OwnerId = ownerId,
                Visibility = visibility
            };
            await policyRepository.InsertAsync(policy);
        }
        else
        {
            if (policy.OwnerId != ownerId)
            {
                throw new UnauthorizedAccessException("Only the resource owner can change its access policy.");
            }

            policy.Visibility = visibility;
            await policyRepository.UpdateAsync(policy);
        }

        var distinctFileIds = fileIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (distinctFileIds.Length > 0)
        {
            var files = await metadataRepository.GetListAsync(file => distinctFileIds.Contains(file.Id)) ?? [];
            if (files.Count != distinctFileIds.Length ||
                files.Any(file => file.CreatorId != ownerId))
            {
                throw new UnauthorizedAccessException(
                    "A resource can reference only managed files owned by the resource owner.");
            }
        }

        var current = await referenceRepository.GetListAsync(
            reference => reference.ResourceType == resourceType && reference.ResourceId == resourceId) ?? [];
        var desired = distinctFileIds.ToHashSet();

        foreach (var stale in current.Where(reference => !desired.Contains(reference.FileId)))
        {
            await referenceRepository.DeleteAsync(stale.Id);
        }

        var currentIds = current.Select(reference => reference.FileId).ToHashSet();
        foreach (var fileId in distinctFileIds.Where(fileId => !currentIds.Contains(fileId)))
        {
            await referenceRepository.InsertAsync(new ResourceFileReferenceEntity
            {
                ResourceType = resourceType,
                ResourceId = resourceId,
                FileId = fileId
            });
        }
    }

    public async Task<bool> CanAccessAsync(
        string resourceType,
        Guid resourceId,
        Guid? userId,
        string? shareToken,
        ResourceAccessContext? accessContext = null,
        CancellationToken cancellationToken = default)
    {
        resourceType = NormalizeResourceType(resourceType);
        var policy = await policyRepository.GetQueryable()
            .FirstAsync(x => x.ResourceType == resourceType && x.ResourceId == resourceId, cancellationToken);

        if (policy == null)
        {
            return false;
        }

        if (policy.Visibility == ResourceVisibility.Public)
        {
            return true;
        }

        if (policy.OwnerId == userId)
        {
            await RecordAuditAsync(
                resourceType,
                resourceId,
                userId,
                granted: true,
                "Owner",
                shareToken,
                shareLinkId: null,
                accessContext,
                fileId: null,
                cancellationToken);
            return true;
        }

        var shareAccess = await HasValidShareAsync(
            resourceType,
            resourceId,
            shareToken,
            userId,
            accessContext,
            countShareView: true,
            cancellationToken);
        await RecordAuditAsync(
            resourceType,
            resourceId,
            userId,
            shareAccess.Granted,
            shareAccess.Granted ? "ShareToken" : "Denied",
            shareToken,
            shareAccess.Granted ? shareAccess.ShareLinkId : null,
            accessContext,
            fileId: null,
            cancellationToken);
        return shareAccess.Granted;
    }

    public async Task<bool> CanAccessFileAsync(
        Guid fileId,
        Guid? userId,
        string? shareToken,
        ResourceAccessContext? accessContext = null,
        CancellationToken cancellationToken = default)
    {
        var file = await metadataRepository.GetAsync(fileId);
        if (file == null)
        {
            return false;
        }

        if (file.CreatorId.HasValue && file.CreatorId == userId)
        {
            return true;
        }

        var references = await referenceRepository.GetListAsync(reference => reference.FileId == fileId) ?? [];
        foreach (var reference in references)
        {
            var resourceType = NormalizeResourceType(reference.ResourceType);
            var policy = await policyRepository.GetQueryable()
                .FirstAsync(x => x.ResourceType == resourceType && x.ResourceId == reference.ResourceId, cancellationToken);
            if (policy == null)
            {
                continue;
            }

            if (policy.Visibility == ResourceVisibility.Public)
            {
                return true;
            }

            if (policy.OwnerId == userId)
            {
                return true;
            }

            var shareAccess = await HasValidShareAsync(
                resourceType,
                reference.ResourceId,
                shareToken,
                userId,
                accessContext,
                countShareView: false,
                cancellationToken);
            if (shareAccess.Granted)
            {
                await RecordAuditAsync(
                    resourceType,
                    reference.ResourceId,
                    userId,
                    granted: true,
                    "ShareToken",
                    shareToken,
                    shareAccess.ShareLinkId,
                    accessContext,
                    fileId,
                    cancellationToken);
                return true;
            }

            await RecordAuditAsync(
                resourceType,
                reference.ResourceId,
                userId,
                granted: false,
                "Denied",
                shareToken,
                null,
                accessContext,
                fileId,
                cancellationToken);
        }

        return false;
    }

    public async Task<ResourceShareLinkDto> CreateShareLinkAsync(
        string resourceType,
        Guid resourceId,
        Guid ownerId,
        TimeSpan lifetime,
        int? maxViews = null,
        CancellationToken cancellationToken = default)
    {
        if (lifetime < TimeSpan.FromHours(1) || lifetime > TimeSpan.FromHours(8760))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                "Share links must be valid for between one hour and 365 days.");
        }

        if (maxViews is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxViews), "Max views must be greater than zero.");
        }

        resourceType = NormalizeResourceType(resourceType);
        var policy = await policyRepository.GetQueryable()
            .FirstAsync(x => x.ResourceType == resourceType && x.ResourceId == resourceId, cancellationToken);
        if (policy == null || policy.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("Only the resource owner can create a share link.");
        }

        if (policy.Visibility != ResourceVisibility.Private)
        {
            throw new ArgumentException("Share links are only needed for private resources.");
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var entity = new ResourceShareLinkEntity
        {
            TokenVersion = CurrentShareTokenVersion,
            ResourceType = resourceType,
            ResourceId = resourceId,
            OwnerId = ownerId,
            TokenHash = HashToken(token),
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            MaxViews = maxViews,
            ViewCount = 0
        };
        await shareRepository.InsertAsync(entity);

        return new ResourceShareLinkDto
        {
            Id = entity.Id,
            Token = token,
            ExpiresAt = entity.ExpiresAt,
            MaxViews = entity.MaxViews
        };
    }

    public async Task RevokeSharesAsync(string resourceType, Guid resourceId, Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        resourceType = NormalizeResourceType(resourceType);
        var policy = await policyRepository.GetQueryable().FirstAsync(
            row => row.ResourceType == resourceType && row.ResourceId == resourceId, cancellationToken);
        if (policy?.OwnerId != ownerId) throw new UnauthorizedAccessException();
        var links = await shareRepository.GetListAsync(row => row.ResourceType == resourceType && row.ResourceId == resourceId);
        foreach (var link in links.Where(link => link.RevokedAt == null))
        {
            link.RevokedAt = DateTime.UtcNow;
            await shareRepository.UpdateAsync(link);
        }
    }

    public async Task DeleteAsync(
        string resourceType,
        Guid resourceId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        resourceType = NormalizeResourceType(resourceType);
        var policy = await policyRepository.GetQueryable()
            .FirstAsync(x => x.ResourceType == resourceType && x.ResourceId == resourceId, cancellationToken);
        if (policy == null)
        {
            return;
        }

        if (policy.OwnerId != ownerId)
        {
            throw new UnauthorizedAccessException("Only the resource owner can delete its access policy.");
        }

        await referenceRepository.DeleteAsync(
            reference => reference.ResourceType == resourceType && reference.ResourceId == resourceId);
        await shareRepository.DeleteAsync(
            link => link.ResourceType == resourceType && link.ResourceId == resourceId);
        await policyRepository.DeleteAsync(policy.Id);
    }

    private async Task<(bool Granted, Guid? ShareLinkId)> HasValidShareAsync(
        string resourceType,
        Guid resourceId,
        string? shareToken,
        Guid? userId,
        ResourceAccessContext? accessContext,
        bool countShareView,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(shareToken))
        {
            return (false, null);
        }

        var tokenHash = HashToken(shareToken);
        var now = DateTime.UtcNow;
        var share = await shareRepository.GetQueryable().FirstAsync(
            link => link.ResourceType == resourceType &&
                    link.ResourceId == resourceId &&
                    link.TokenHash == tokenHash,
            cancellationToken);

        if (share == null ||
            share.TokenVersion != CurrentShareTokenVersion ||
            share.RevokedAt != null ||
            share.ExpiresAt <= now)
        {
            return (false, share?.Id);
        }

        var fingerprintHash = HashFingerprint(accessContext?.VisitorFingerprint);
        var hasFingerprint = !string.IsNullOrWhiteSpace(fingerprintHash);
        if (hasFingerprint)
        {
            var existingViewer = await shareViewerRepository.GetQueryable().FirstAsync(
                viewer => viewer.ShareLinkId == share.Id &&
                          viewer.FingerprintHash == fingerprintHash,
                cancellationToken);
            if (existingViewer != null)
            {
                existingViewer.LastAccessedAt = now;
                await shareViewerRepository.UpdateAsync(existingViewer);
                return (true, share.Id);
            }
        }

        if (share.MaxViews.HasValue &&
            (!hasFingerprint || share.ViewCount >= share.MaxViews.Value))
        {
            return (false, share.Id);
        }

        if (countShareView && hasFingerprint)
        {
            await shareViewerRepository.InsertAsync(new ResourceShareLinkViewerEntity
            {
                Id = Guid.NewGuid(),
                ShareLinkId = share.Id,
                FingerprintHash = fingerprintHash!,
                ActorUserId = userId,
                FirstAccessedAt = now,
                LastAccessedAt = now,
                ClientIp = accessContext?.ClientIp
            });

            share.ViewCount++;
            await shareRepository.UpdateAsync(share);
        }

        return (true, share.Id);
    }

    private async Task RecordAuditAsync(
        string resourceType,
        Guid resourceId,
        Guid? userId,
        bool granted,
        string accessReason,
        string? shareToken,
        Guid? shareLinkId,
        ResourceAccessContext? accessContext,
        Guid? fileId,
        CancellationToken cancellationToken)
    {
        if (accessContext == null)
        {
            return;
        }

        await auditService.RecordAccessAsync(
            resourceType,
            resourceId,
            userId,
            granted,
            accessReason,
            string.IsNullOrWhiteSpace(shareToken) ? null : HashToken(shareToken),
            fileId,
            accessContext.ClientIp,
            accessContext.UserAgent,
            shareLinkId,
            HashFingerprint(accessContext.VisitorFingerprint),
            cancellationToken);
    }

    private static string NormalizeResourceType(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 64)
        {
            throw new ArgumentException("Resource type is required and cannot exceed 64 characters.", nameof(value));
        }

        return normalized;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string? HashFingerprint(string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint) || !IsValidFingerprint(fingerprint))
        {
            return null;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint.Trim().ToLowerInvariant())));
    }

    private static bool IsValidFingerprint(string? value)
    {
        return value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
