using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.Permission;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.Permission;
using Sim.System.Domain.Role;

namespace Sim.System.Application.Permission;

[Authorize]
public class PermissionService(
    IPermissionRepository permissionRepository,
    IRolePermissionRepository rolePermissionRepository,
    IAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor)
    : CrudAppService<Guid, PermissionEntity, PermissionDto, CreatePermissionDto, UpdatePermissionDto>(
        permissionRepository), IPermissionService
{
    /// <summary>
    /// 获取指定权限的详情。
    /// </summary>
    public override async Task<PermissionDto> GetAsync(Guid id)
    {
        await EnsureCanReadCatalogAsync();
        return await base.GetAsync(id);
    }

    /// <summary>
    /// 分页获取权限列表。
    /// </summary>
    public override async Task<PagedResultDto<PermissionDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        await EnsureCanReadCatalogAsync();
        return await base.GetListAsync(input);
    }

    /// <summary>
    /// 创建权限。
    /// </summary>
    [Permission(SystemPermissions.Permission.Manage)]
    public override Task<PermissionDto> CreateAsync(CreatePermissionDto input) => base.CreateAsync(input);

    /// <summary>
    /// 更新指定权限。
    /// </summary>
    [Permission(SystemPermissions.Permission.Manage)]
    public override Task<PermissionDto> UpdateAsync(UpdatePermissionDto input) => base.UpdateAsync(input);

    /// <summary>
    /// 删除指定权限。
    /// </summary>
    [Permission(SystemPermissions.Permission.Manage)]
    public override Task<bool> DeleteAsync(Guid id) => base.DeleteAsync(id);

    /// <summary>
    /// 获取指定角色已分配的权限列表。
    /// </summary>
    public async Task<List<PermissionDto>> GetPermissionsByRoleAsync(Guid roleId)
    {
        await EnsureCanReadCatalogAsync();

        // 获取角色关联的权限ID
        var rolePermissions = await rolePermissionRepository.GetListAsync(rp => rp.RoleId == roleId);
        var permissionIds = rolePermissions.Select(rp => rp.PermissionId).ToList();

        // 批量获取权限信息
        var permissions = await permissionRepository.GetListAsync(p => permissionIds.Contains(p.Id));

        // 转换为DTO
        var permissionDtos = permissions.Select(MapToDto).ToList();

        return permissionDtos;
    }

    /// <summary>
    /// 权限目录同时服务于权限管理页、角色授权页和路由编辑页：
    /// 角色管理员需要清单才能为角色分配权限，路由管理员需要清单才能配置路由权限码。
    /// </summary>
    private async Task EnsureCanReadCatalogAsync()
    {
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new AuthorizationException();

        string[] allowedCodes =
        [
            SystemPermissions.Permission.Manage,
            SystemPermissions.Role.Manage,
            SystemPermissions.RouteConfig.Create,
            SystemPermissions.RouteConfig.Update,
            SystemPermissions.RouteConfig.Delete
        ];

        foreach (var code in allowedCodes)
        {
            var result = await authorizationService.AuthorizeAsync(
                httpContext.User,
                null,
                new PermissionAttribute(code));
            if (result.Succeeded)
            {
                return;
            }
        }

        throw new ForbiddenException(
            SystemErrorCodes.PermissionCatalogAccessDenied,
            "Reading the permission catalog requires the permission, role, or route management permission.");
    }
}
