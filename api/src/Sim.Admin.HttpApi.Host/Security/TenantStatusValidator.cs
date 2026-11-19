using System;
using System.Threading;
using System.Threading.Tasks;
using Sim.Framework.Domain.Shared.Security;
using SqlSugar;
using Sim.System.Domain.Tenant;

namespace Sim.Admin.HttpApi.Host.Security;

/// <summary>
/// Reads the host-level tenant registry directly so the result is not affected by
/// the per-request tenant query filter.
/// </summary>
public sealed class TenantStatusValidator(ISqlSugarClient database) : ITenantStatusValidator
{
    public async Task<bool> IsActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await database.Queryable<TenantEntity>()
            .Where(tenant => tenant.Id == tenantId && tenant.IsActive)
            .AnyAsync(cancellationToken);
    }
}
