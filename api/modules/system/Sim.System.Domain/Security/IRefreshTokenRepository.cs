using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.System.Domain.Security;

public interface IRefreshTokenRepository : IRepository<RefreshTokenEntity>, ISqlSugarRepository<RefreshTokenEntity>
{
    Task<bool> TryRotateAsync(Guid tokenId, string replacementTokenHash, DateTime revokedAt);
    Task<int> RevokeFamilyAsync(Guid userId, Guid familyId, DateTime revokedAt);
    Task<int> RevokeFamiliesAsync(Guid userId, IReadOnlyList<Guid> familyIds, DateTime revokedAt);
    Task<List<RefreshTokenEntity>> GetActiveSessionTokensAsync(Guid userId, DateTime utcNow);
    Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, DateTime utcNow);
    Task TouchSessionAsync(Guid userId, Guid sessionId, DateTime lastSeenAt, DateTime touchBefore);
}
