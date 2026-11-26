using System;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.Permission;

namespace Sim.System.SqlSugar.Repositories.Permission;

public class PermissionRepository(ISqlSugarClient db)
    : SqlSugarRepository<PermissionEntity>(db), IPermissionRepository
{
    // Initialization logic specific to PermissionRepository can be added here
}