using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.Role;

public interface IRolePermissionRepository : IRepository<RolePermission>, ISqlSugarRepository<RolePermission>
{

}