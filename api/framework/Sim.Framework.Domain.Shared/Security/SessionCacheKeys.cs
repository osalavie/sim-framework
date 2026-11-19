namespace Sim.Framework.Domain.Shared.Security;

/// <summary>
/// Shared keys for the per-request session validation cache.
/// </summary>
public static class SessionCacheKeys
{
    public static string ForSession(Guid userId, Guid sessionId) =>
        $"jwt:session:{userId}:{sessionId}";
}
