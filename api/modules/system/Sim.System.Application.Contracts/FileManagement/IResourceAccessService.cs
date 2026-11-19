using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.FileManagement;

/// <summary>
/// Generic owner/public policy port for business resources. Private resources can be
/// accessed by their owner or by a valid, non-revoked, non-expired share link.
/// </summary>
public interface IResourceAccessService
{
    Task ConfigureAsync(
        string resourceType,
        Guid resourceId,
        Guid ownerId,
        ResourceVisibility visibility,
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessAsync(
        string resourceType,
        Guid resourceId,
        Guid? userId,
        string? shareToken,
        ResourceAccessContext? accessContext = null,
        CancellationToken cancellationToken = default);

    Task<bool> CanAccessFileAsync(
        Guid fileId,
        Guid? userId,
        string? shareToken,
        ResourceAccessContext? accessContext = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a one-time-configurable capability link. The raw token is returned only here.
    /// </summary>
    Task<ResourceShareLinkDto> CreateShareLinkAsync(
        string resourceType,
        Guid resourceId,
        Guid ownerId,
        TimeSpan lifetime,
        int? maxViews = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string resourceType,
        Guid resourceId,
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task RevokeSharesAsync(string resourceType, Guid resourceId, Guid ownerId,
        CancellationToken cancellationToken = default);

    Task EnsureEncryptedFilesAsync(Guid ownerId, IReadOnlyCollection<Guid> fileIds);
}
