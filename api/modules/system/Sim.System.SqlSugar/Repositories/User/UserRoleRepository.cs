using System;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.User;

namespace Sim.System.SqlSugar.Repositories.User;

public class UserRoleRepository(ISqlSugarClient db)
    : SqlSugarRepository<UserRole>(db), IUserRoleRepository
{
    // Initialization logic specific to UserRoleRepository can be added here
}