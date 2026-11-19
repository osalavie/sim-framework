namespace Sim.System.Domain.Shared.Permissions;

/// <summary>
/// 路由访问权限码匹配：空权限码表示不限制，多个权限码按 <c>requireAll</c> 决定任一或全部满足。
/// </summary>
public static class RoutePermissionPolicy
{
    /// <summary>超级权限码，任意路由权限均可满足。</summary>
    public const string SuperPermissionCode = "*:*:*";

    public static bool CanAccess(
        string? requiredCodes,
        IReadOnlySet<string> grantedCodes,
        bool requireAll)
    {
        if (string.IsNullOrWhiteSpace(requiredCodes) || grantedCodes.Contains(SuperPermissionCode))
        {
            return true;
        }

        var required = requiredCodes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (required.Length == 0)
        {
            return true;
        }

        return requireAll
            ? required.All(grantedCodes.Contains)
            : required.Any(grantedCodes.Contains);
    }
}
