using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.Tenant;

namespace Sim.System.SqlSugar.Repositories.Tenant;

public class TenantRepository : SqlSugarRepository<TenantEntity>, ITenantRepository
{
    public TenantRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<TenantEntity?> FindByCodeAsync(string code)
    {
        return await base.GetAsync(t => t.Code == code);
    }
}
