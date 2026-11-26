namespace Sim.Framework.Domain.Shared.Security;

/// <summary>
/// Current tenant abstraction. Replaces Volo.Abp.MultiTenancy.ICurrentTenant.
/// Provides access to the tenant ID resolved from the request context.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>Current tenant ID. Null means host-level (no tenant filter).</summary>
    Guid? Id { get; }

    /// <summary>Tenant name (optional, for display purposes).</summary>
    string? Name { get; }
}
