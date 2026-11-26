
using Sim.SqlSugar.Core.Repositories;
using Sim.Framework.Domain;

namespace Sim.System.Domain.ExternalLogin;

public interface IExternalLoginRepository
    : IRepository<ExternalLoginEntity>, ISqlSugarRepository<ExternalLoginEntity>
{
    Task<ExternalLoginEntity?> GetAsync(string provider, string providerKey);

    Task<List<ExternalLoginEntity>> GetListByUserIdAsync(Guid userId);
}
