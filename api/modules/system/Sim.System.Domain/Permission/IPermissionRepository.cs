using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.Permission;

public interface IPermissionRepository : IRepository<PermissionEntity>, ISqlSugarRepository<PermissionEntity>
{

}