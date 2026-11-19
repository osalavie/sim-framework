namespace Sim.OpenPlatform.Domain.Shared;

/// <summary>
/// 开放平台授权范围（Scope）策略。
/// 第三方应用只能被授予系统权限目录中的业务权限：
/// - 超级权限 `*:*:*` 等价于跳过全部权限校验，永远不可授予；
/// - 开放平台自身权限不可授予，避免应用用换取的 Token 反向管理接入应用（自提权）。
/// </summary>
public static class OpenPlatformScopePolicy
{
    /// <summary>超级权限码，与数据种子 DataSeedConsts.SuperPermissionCode 保持一致。</summary>
    public const string SuperPermissionCode = "*:*:*";

    /// <summary>开放平台自身权限码前缀。</summary>
    public const string OpenPlatformCodePrefix = "openplatform:";

    /// <summary>判断权限码是否可以作为第三方应用的授权范围。</summary>
    public static bool IsGrantable(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();

        return !normalized.Equals(SuperPermissionCode, StringComparison.Ordinal)
               && !normalized.StartsWith(OpenPlatformCodePrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool AreGrantable(IEnumerable<string>? codes, IReadOnlySet<string> availableCodes) =>
        codes is not null && codes.All(code => IsGrantable(code) && availableCodes.Contains(code));
}
