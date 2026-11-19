using Sim.OpenPlatform.Application.Contracts.Dtos;

namespace Sim.OpenPlatform.Application.Contracts;

/// <summary>
/// 权限目录端口 — 由组合根实现（组合根可访问 System 模块的权限仓储），
/// 向管理端提供第三方应用可授予的授权范围。
/// </summary>
public interface IOpenPlatformScopeCatalog
{
    /// <summary>获取第三方应用可授予的权限范围（已过滤保留权限）。</summary>
    Task<List<OpenPlatformScopeDto>> GetGrantableScopesAsync();
}
