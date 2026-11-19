using Sim.System.FileManagement;

namespace Sim.System.Application.Contracts.FileManagement;

/// <summary>
/// Enforces storage quotas per tenant and/or per user.
/// </summary>
public interface IStorageQuotaService
{
    /// <summary>
    /// Get the current storage usage and quota for a tenant or user.
    /// </summary>
    Task<StorageQuotaDto> GetUsageAsync(
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if uploading a file of the specified size would exceed the quota.
    /// Returns true if allowed, false if the quota would be exceeded.
    /// </summary>
    Task<bool> CheckQuotaAsync(
        long additionalBytes,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set the storage quota for a tenant or user.
    /// </summary>
    Task SetQuotaAsync(
        long quotaBytes,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
