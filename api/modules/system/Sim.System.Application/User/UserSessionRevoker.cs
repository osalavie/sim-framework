using Microsoft.Extensions.Caching.Memory;
using Sim.Framework.Domain.Shared.Security;
using Sim.System.Domain.Security;

namespace Sim.System.Application.User;

public interface IUserSessionRevoker
{
    Task RevokeUsersAsync(IReadOnlyCollection<Guid> userIds);

    Task<int> RevokeSessionAsync(Guid userId, Guid sessionId);
}

/// <summary>
/// 权限/角色变更后吊销目标用户的刷新令牌。JWT 的会话校验会随后拒绝其访问令牌，
/// 因此变更在会话缓存过期（30 秒）内生效，无需等待访问令牌自然过期。
/// </summary>
public sealed class UserSessionRevoker(
    IRefreshTokenRepository refreshTokenRepository,
    IMemoryCache sessionCache) : IUserSessionRevoker
{
    public async Task RevokeUsersAsync(IReadOnlyCollection<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var tokens = await refreshTokenRepository.GetListAsync(
            token => ids.Contains(token.UserId) && !token.IsRevoked);
        if (tokens.Count == 0)
        {
            return;
        }

        var revokedAt = DateTime.UtcNow;
        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = revokedAt;
            await refreshTokenRepository.UpdateAsync(token);
            sessionCache.Remove(SessionCacheKeys.ForSession(token.UserId, token.FamilyId));
        }
    }

    public async Task<int> RevokeSessionAsync(Guid userId, Guid sessionId)
    {
        var affected = await refreshTokenRepository.RevokeFamilyAsync(
            userId,
            sessionId,
            DateTime.UtcNow);
        sessionCache.Remove(SessionCacheKeys.ForSession(userId, sessionId));
        return affected;
    }
}
