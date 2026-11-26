using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.Tenant;

public interface ITenantRepository : ISqlSugarRepository<TenantEntity>
{
    /// <summary>按 Code 查找租户</summary>
    Task<TenantEntity?> FindByCodeAsync(string code);
}
