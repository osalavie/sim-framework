using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.ExternalLogin;

namespace Sim.System.SqlSugar.Repositories.ExternalLogin;

public class ExternalLoginRepository(
    ISqlSugarClient db)
    : SqlSugarRepository<ExternalLoginEntity>(db), IExternalLoginRepository
{
    public Task<ExternalLoginEntity?> GetAsync(string provider, string providerKey)
    {
        return GetAsync(entity =>
            entity.Provider == provider &&
            entity.ProviderKey == providerKey);
    }

    public Task<List<ExternalLoginEntity>> GetListByUserIdAsync(Guid userId)
    {
        return GetListAsync(entity => entity.UserId == userId);
    }
}
