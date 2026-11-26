using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.Role;

public interface IRoleModuleRepository : IRepository<RoleModule>, ISqlSugarRepository<RoleModule>
{
}
