using System;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.Role;

namespace Sim.System.SqlSugar.Repositories.Role;

public class RoleRepository(ISqlSugarClient db)
    : SqlSugarRepository<RoleEntity>(db), IRoleRepository
{
    // Initialization logic specific to RoleRepository can be added here
}