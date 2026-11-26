using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.OpenPlatform.Application.Contracts;
using Sim.OpenPlatform.Application.Contracts.Dtos;
using Sim.OpenPlatform.Domain.Shared;

namespace Sim.OpenPlatform.Application.Services;

/// <summary>
/// 开放平台授权范围 — 返回管理端可选择的权限码目录（只读，不允许第三方应用自行维护）。
/// </summary>
[Authorize]
[Permission(OpenPlatformPermissions.Manage)]
public class OpenPlatformScopeAppService : ApplicationService, IOpenPlatformScopeAppService
{
    private readonly IOpenPlatformScopeCatalog _scopeCatalog;

    public OpenPlatformScopeAppService(IOpenPlatformScopeCatalog scopeCatalog)
    {
        _scopeCatalog = scopeCatalog;
    }

    /// <summary>获取可授予第三方应用的权限范围列表（只读权限目录）。</summary>
    /// <returns>权限范围 DTO 列表。</returns>
    public Task<List<OpenPlatformScopeDto>> GetListAsync() => _scopeCatalog.GetGrantableScopesAsync();
}
