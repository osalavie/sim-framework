namespace Sim.Framework.Domain.Shared.Security;

/// <summary>
/// Validates whether a tenant is still active. Implemented at the composition root
/// because System must not depend on the Tenant module's domain.
/// </summary>
public interface ITenantStatusValidator
{
    Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
