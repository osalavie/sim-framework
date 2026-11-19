using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.Role;

namespace Sim.System.SqlSugar.Repositories.Role;

public class RoleModuleRepository(ISqlSugarClient db)
    : SqlSugarRepository<RoleModule>(db), IRoleModuleRepository
{
}
