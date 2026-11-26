using Sim.OpenPlatform.Application.Contracts;
using Sim.OpenPlatform.Application.Contracts.Dtos;
using Sim.OpenPlatform.Domain.Shared;
using Sim.System.Domain.Permission;

namespace Sim.Admin.Domain.Services;

/// <summary>
/// 开放平台授权范围目录 — 实现在组合根，读取 System 模块的权限目录，
/// 过滤掉超级权限与开放平台自身权限后提供给管理端选择。
/// </summary>
public class OpenPlatformScopeCatalog : IOpenPlatformScopeCatalog
{
    private readonly IPermissionRepository _permissionRepository;

    public OpenPlatformScopeCatalog(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<List<OpenPlatformScopeDto>> GetGrantableScopesAsync()
    {
        var permissions = await _permissionRepository.GetAll();

        return permissions
            .Where(permission => OpenPlatformScopePolicy.IsGrantable(permission.Code))
            .OrderBy(permission => permission.Code, StringComparer.Ordinal)
            .Select(permission => new OpenPlatformScopeDto
            {
                Code = permission.Code,
                Name = permission.Name,
                Description = permission.Description
            })
            .ToList();
    }
}
