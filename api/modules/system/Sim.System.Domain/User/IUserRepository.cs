using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.User;

public interface IUserRepository : IRepository<UserEntity>, ISqlSugarRepository<UserEntity>
{
    Task<bool> TryConsumeSecurityChallengeAsync(Guid userId, Guid challengeId, DateTime utcNow);
}
