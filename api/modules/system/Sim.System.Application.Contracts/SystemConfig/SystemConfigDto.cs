namespace Sim.System.Application.Contracts.SystemConfig;

/// <summary>
/// 系统配置 DTO
/// </summary>
public class SystemConfigDto
{
    /// <summary>
    /// 角色配置
    /// </summary>
    public RoleConfigDto Roles { get; set; } = new();
}

/// <summary>
/// 角色配置 DTO
/// </summary>
public class RoleConfigDto
{
    /// <summary>
    /// 管理员角色代码
    /// </summary>
    public string AdminRole { get; set; } = string.Empty;

    /// <summary>
    /// 普通用户角色代码
    /// </summary>
    public string UserRole { get; set; } = string.Empty;
}
