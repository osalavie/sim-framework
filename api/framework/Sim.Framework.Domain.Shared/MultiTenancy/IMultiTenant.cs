namespace Sim.Framework.Domain.Shared.MultiTenancy;

/// <summary>
/// Marker interface for multi-tenant entities. Replaces Volo.Abp.MultiTenancy.IMultiTenant.
/// Entities implementing this will be automatically filtered by TenantId in queries
/// and auto-populated on insert.
/// </summary>
public interface IMultiTenant
{
    /// <summary>Tenant identifier. Null means host-level (no tenant isolation).</summary>
    Guid? TenantId { get; set; }
}
