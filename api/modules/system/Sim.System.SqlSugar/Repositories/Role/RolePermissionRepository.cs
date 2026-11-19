using System;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.Role;

namespace Sim.System.SqlSugar.Repositories.Role;

public class RolePermissionRepository(ISqlSugarClient db)
    : SqlSugarRepository<RolePermission>(db), IRolePermissionRepository,
        ISqlSugarRepository<RolePermission>
{
    // Initialization logic specific to RolePermissionRepository can be added here
}