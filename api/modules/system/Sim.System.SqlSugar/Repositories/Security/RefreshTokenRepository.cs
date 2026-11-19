using Sim.SqlSugar.Core.Repositories;
using SqlSugar;
using Sim.System.Domain.Security;

namespace Sim.System.SqlSugar.Repositories.Security;

public class RefreshTokenRepository : SqlSugarRepository<RefreshTokenEntity>, IRefreshTokenRepository
{
    public RefreshTokenRepository(ISqlSugarClient sqlSugarClient) : base(sqlSugarClient)
    {
    }

    public async Task<bool> TryRotateAsync(Guid tokenId, string replacementTokenHash, DateTime revokedAt)
    {
        var affectedRows = await AsTenant().UpdateableWithAttr<RefreshTokenEntity>()
            .SetColumns(token => token.IsRevoked == true)
            .SetColumns(token => token.RevokedAt == revokedAt)
            .SetColumns(token => token.ReplacedByToken == replacementTokenHash)
            .Where(token => token.Id == tokenId && !token.IsRevoked && token.ExpiresAt > revokedAt)
            .ExecuteCommandAsync();

        return affectedRows == 1;
    }

    public Task<int> RevokeFamilyAsync(Guid userId, Guid familyId, DateTime revokedAt)
    {
        return AsTenant().UpdateableWithAttr<RefreshTokenEntity>()
            .SetColumns(token => token.IsRevoked == true)
            .SetColumns(token => token.RevokedAt == revokedAt)
            .Where(token => token.UserId == userId && token.FamilyId == familyId && !token.IsRevoked)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 批量吊销多个 session family，使用单条 UPDATE 减少 DB 往返。
    /// </summary>
    public async Task<int> RevokeFamiliesAsync(Guid userId, IReadOnlyList<Guid> familyIds, DateTime revokedAt)
    {
        if (familyIds == null || familyIds.Count == 0)
            return 0;

        return await AsTenant().UpdateableWithAttr<RefreshTokenEntity>()
            .SetColumns(token => token.IsRevoked == true)
            .SetColumns(token => token.RevokedAt == revokedAt)
            .Where(token => token.UserId == userId && familyIds.Contains(token.FamilyId) && !token.IsRevoked)
            .ExecuteCommandAsync();
    }

    public Task<List<RefreshTokenEntity>> GetActiveSessionTokensAsync(Guid userId, DateTime utcNow)
    {
        return GetQueryable()
            .Where(token => token.UserId == userId && !token.IsRevoked && token.ExpiresAt > utcNow)
            .ToListAsync();
    }

    public Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, DateTime utcNow)
    {
        return GetQueryable()
            .AnyAsync(token =>
                token.UserId == userId &&
                token.FamilyId == sessionId &&
                !token.IsRevoked &&
                token.ExpiresAt > utcNow);
    }

    public async Task TouchSessionAsync(
        Guid userId,
        Guid sessionId,
        DateTime lastSeenAt,
        DateTime touchBefore)
    {
        await AsTenant().UpdateableWithAttr<RefreshTokenEntity>()
            .SetColumns(token => token.LastSeenAt == lastSeenAt)
            .Where(token =>
                token.UserId == userId &&
                token.FamilyId == sessionId &&
                !token.IsRevoked &&
                token.ExpiresAt > lastSeenAt &&
                (token.LastSeenAt == null || token.LastSeenAt < touchBefore))
            .ExecuteCommandAsync();
    }
}
