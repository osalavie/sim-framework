using Sim.Framework.Application.Contracts.IServices;
using Sim.OpenPlatform.Application.Contracts.Dtos;

namespace Sim.OpenPlatform.Application.Contracts;

/// <summary>
/// 开放平台授权范围 — 供管理端以选择方式配置第三方应用权限。
/// </summary>
public interface IOpenPlatformScopeAppService : IApplicationService
{
    /// <summary>获取可授予的权限范围列表。</summary>
    Task<List<OpenPlatformScopeDto>> GetListAsync();
}
