namespace Sim.System.Application.Contracts.User;

/// <summary>
/// 登录响应
/// </summary>
public class LoginUserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public List<RoleInfo>? Roles { get; set; }
    public List<PermissionInfo>? Permissions { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public bool RequiresTwoFactor { get; set; }
    public Guid? TwoFactorChallengeId { get; set; }
    public IReadOnlyList<string> TwoFactorMethods { get; set; } = [];

    // 外部登录注册模式字段
    public bool RequiresExternalLoginRegistration { get; set; }

    /// <summary>站点是否开放自助注册；关闭时只能关联已有账号。</summary>
    public bool ExternalLoginRegistrationAllowed { get; set; }
    public string? ExternalLoginProvider { get; set; }
    public string? ExternalLoginProviderDisplayName { get; set; }
    public string? ExternalLoginUserName { get; set; }
    public string? ExternalLoginDisplayName { get; set; }
    public string? ExternalLoginEmail { get; set; }
    public string? ExternalLoginRegistrationToken { get; set; }
    public bool ExternalLoginBindingCompleted { get; set; }
}

/// <summary>
/// 角色信息
/// </summary>
public class RoleInfo
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 权限信息
/// </summary>
public class PermissionInfo
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
