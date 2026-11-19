using System;
using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.User;

namespace Sim.System.SqlSugar.Repositories.User;

public class UserRepository(ISqlSugarClient db)
    : SqlSugarRepository<UserEntity>(db), IUserRepository
{
    public async Task<bool> TryConsumeSecurityChallengeAsync(
        Guid userId,
        Guid challengeId,
        DateTime utcNow)
    {
        var affectedRows = await AsTenant().UpdateableWithAttr<UserEntity>()
            .SetColumns(user => user.SecurityChallengeId == null)
            .SetColumns(user => user.SecurityChallengePurpose == null)
            .SetColumns(user => user.SecurityChallengeMethod == null)
            .SetColumns(user => user.SecurityChallengeCodeHash == null)
            .SetColumns(user => user.SecurityChallengeSalt == null)
            .SetColumns(user => user.SecurityChallengeExpiresAt == null)
            .SetColumns(user => user.SecurityChallengeAttempts == 0)
            .Where(user =>
                user.Id == userId &&
                user.SecurityChallengeId == challengeId &&
                user.SecurityChallengeExpiresAt > utcNow)
            .ExecuteCommandAsync();
        return affectedRows == 1;
    }
}
