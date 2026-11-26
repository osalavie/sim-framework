using Sim.Framework.Domain.Entities;
using SqlSugar;

namespace Sim.System.Domain.FileManagement;

/// <summary>
/// Storage quota configuration per tenant or user.
/// </summary>
[SugarTable("system_storage_quota")]
[SugarIndex("uk_storage_quota_tenant_user", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, true)]
public sealed class StorageQuotaEntity : AuditedEntity
{
    /// <summary>
    /// Tenant ID. Null means global/default quota.
    /// </summary>
    public new Guid? TenantId { get; set; }

    /// <summary>
    /// User ID. Null means tenant-level quota.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Maximum allowed storage in bytes. 0 means unlimited.
    /// </summary>
    public long QuotaBytes { get; set; }

    /// <summary>
    /// Current used storage in bytes (denormalized for performance).
    /// </summary>
    public long UsedBytes { get; set; }

    /// <summary>
    /// Current file count (denormalized for performance).
    /// </summary>
    public int FileCount { get; set; }
}
