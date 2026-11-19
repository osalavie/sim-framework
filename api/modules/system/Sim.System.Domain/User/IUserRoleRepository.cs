using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.User;

public interface IUserRoleRepository : IRepository<UserRole>, ISqlSugarRepository<UserRole>
{

}