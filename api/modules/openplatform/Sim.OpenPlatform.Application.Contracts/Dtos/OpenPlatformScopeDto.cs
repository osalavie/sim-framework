namespace Sim.OpenPlatform.Application.Contracts.Dtos;

/// <summary>
/// 可授予第三方应用的权限范围，来自系统权限目录，仅供管理端选择，只读。
/// </summary>
public class OpenPlatformScopeDto
{
    /// <summary>权限码，例如 blog:article:read。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>权限名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>权限说明。</summary>
    public string? Description { get; set; }
}
