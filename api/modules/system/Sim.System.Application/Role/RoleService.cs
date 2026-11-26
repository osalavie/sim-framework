using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.Permission;
using Sim.System.Application.Contracts.Role;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.User;
using Sim.System.Domain.Organization;
using Sim.System.Domain.Permission;
using Sim.System.Domain.Role;
using Sim.System.Domain.User;

namespace Sim.System.Application.Role;

[Permission(SystemPermissions.Role.Manage)]
public class RoleService(
    IRoleRepository roleRepository,
    IRolePermissionRepository rolePermissionRepository,
    IPermissionRepository permissionRepository,
    IUserRoleRepository userRoleRepository,
    IRoleModuleRepository roleModuleRepository,
    ISqlSugarRepository<RoleDataScopeEntity> roleDataScopeRepository,
    IUserSessionRevoker sessionRevoker)
    : CrudAppService<Guid, RoleEntity, RoleDto, CreateRoleDto, UpdateRoleDto>(roleRepository), IRoleService
{
    /// <summary>
    /// 获取指定角色的详情。
    /// </summary>
    public override Task<RoleDto> GetAsync(Guid id) => base.GetAsync(id);

    /// <summary>
    /// 分页获取角色列表。
    /// </summary>
    public override Task<PagedResultDto<RoleDto>> GetListAsync(PagedAndSortedResultRequestDto input) =>
        base.GetListAsync(input);

    /// <summary>
    /// 创建角色。
    /// </summary>
    public override async Task<RoleDto> CreateAsync(CreateRoleDto input)
    {
        await EnsureCodeIsUniqueAsync(input.Code);
        return await base.CreateAsync(input);
    }

    /// <summary>
    /// 更新指定角色；角色编码变化时吊销该角色下用户的登录会话。
    /// </summary>
    [SqlSugarUnitOfWork]
    public override async Task<RoleDto> UpdateAsync(UpdateRoleDto input)
    {
        var existing = await roleRepository.GetAsync(input.Id)
            ?? throw new BusinessException(SystemErrorCodes.RoleNotFound, "The role does not exist.");

        EnsureRoleCodeIsMutable(existing, input.Code);

        var codeChanged = !string.Equals(existing.Code, input.Code, StringComparison.Ordinal);
        if (codeChanged)
        {
            await EnsureCodeIsUniqueAsync(input.Code, input.Id);
        }

        var result = await base.UpdateAsync(input);

        if (codeChanged)
        {
            await RevokeSessionsOfRoleUsersAsync(input.Id);
        }

        return result;
    }

    /// <summary>
    /// 删除指定角色及其关联数据；内置角色不可删除。
    /// </summary>
    [SqlSugarUnitOfWork]
    public override async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await roleRepository.GetAsync(id)
            ?? throw new BusinessException(SystemErrorCodes.RoleNotFound, "The role does not exist.");

        if (existing.IsBuiltIn)
        {
            throw new BusinessException(
                SystemErrorCodes.BuiltInRoleProtected,
                "The built-in role cannot be deleted.");
        }

        var userIds = await GetRoleUserIdsAsync(id);

        await rolePermissionRepository.DeleteAsync(link => link.RoleId == id);
        await roleModuleRepository.DeleteAsync(link => link.RoleId == id);
        await userRoleRepository.DeleteAsync(link => link.RoleId == id);
        await roleDataScopeRepository.DeleteAsync(scope => scope.RoleId == id);

        var result = await base.DeleteAsync(id);
        await sessionRevoker.RevokeUsersAsync(userIds);
        return result;
    }

    /// <summary>
    /// 为角色分配权限。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task AssignRolePermissionsAsync(Guid roleId, List<Guid> permissionIds)
    {
        var role = await roleRepository.GetAsync(roleId)
            ?? throw new BusinessException(SystemErrorCodes.RoleNotFound, "The role does not exist.");

        EnsureRoleIsMutable(role);

        var distinctIds = permissionIds.Distinct().ToList();
        if (distinctIds.Count > 0)
        {
            var permissions = await permissionRepository.GetListAsync(
                permission => distinctIds.Contains(permission.Id));
            if (permissions.Count != distinctIds.Count)
            {
                throw new BusinessException(
                    SystemErrorCodes.PermissionNotFound,
                    "One or more permissions do not exist.");
            }
        }

        var existingLinks = await rolePermissionRepository.GetListAsync(link => link.RoleId == roleId);
        var existingIds = existingLinks.Select(link => link.PermissionId).OrderBy(id => id).ToList();
        var orderedIds = distinctIds.OrderBy(id => id).ToList();
        if (existingIds.SequenceEqual(orderedIds))
        {
            return;
        }

        if (existingLinks.Count > 0)
        {
            await rolePermissionRepository.DeleteManyAsync(existingLinks);
        }

        foreach (var permissionId in distinctIds)
        {
            await rolePermissionRepository.InsertAsync(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });
        }

        await RevokeSessionsOfRoleUsersAsync(roleId);
    }

    private static void EnsureRoleIsMutable(RoleEntity role)
    {
        if (role.IsBuiltIn)
        {
            throw new BusinessException(
                    SystemErrorCodes.BuiltInRoleProtected,
                    "The built-in role cannot be edited.")
                .WithData("RoleId", role.Id);
        }
    }

    private static void EnsureRoleCodeIsMutable(RoleEntity role, string code)
    {
        if (role.IsBuiltIn && !string.Equals(role.Code, code, StringComparison.Ordinal))
        {
            throw new BusinessException(
                    SystemErrorCodes.BuiltInRoleProtected,
                    "The code of the built-in role cannot be changed.")
                .WithData("RoleId", role.Id);
        }
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? excludedRoleId = null)
    {
        var duplicate = excludedRoleId.HasValue
            ? await roleRepository.GetAsync(role => role.Code == code && role.Id != excludedRoleId.Value)
            : await roleRepository.GetAsync(role => role.Code == code);

        if (duplicate != null)
        {
            throw new BusinessException(
                SystemErrorCodes.RoleCodeAlreadyExists,
                "The role code is already taken.");
        }
    }

    private async Task<List<Guid>> GetRoleUserIdsAsync(Guid roleId)
    {
        var links = await userRoleRepository.GetListAsync(link => link.RoleId == roleId);
        return links.Select(link => link.UserId).Distinct().ToList();
    }

    private async Task RevokeSessionsOfRoleUsersAsync(Guid roleId)
    {
        var userIds = await GetRoleUserIdsAsync(roleId);
        await sessionRevoker.RevokeUsersAsync(userIds);
    }
}
