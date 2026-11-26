using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.RoleModule;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;
using Sim.Framework.Core.UnitOfWork;
using Sim.System.Domain.Shared.ErrorCodes;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Domain.Role;

namespace Sim.System.Application.Role;

[Permission(SystemPermissions.RoleModule.Manage)]
public class RoleModuleService : ApplicationService, IRoleModuleService
{
    private readonly IRoleModuleRepository _roleModuleRepository;
    private readonly IRoleRepository _roleRepository;

    public RoleModuleService(
        IRoleModuleRepository roleModuleRepository,
        IRoleRepository roleRepository)
    {
        _roleModuleRepository = roleModuleRepository;
        _roleRepository = roleRepository;
    }

    /// <summary>
    /// 为角色分配模块
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task AssignModulesToRoleAsync(Guid roleId, List<Guid> moduleIds)
    {
        await EnsureRoleIsMutableAsync(roleId);

        // 清空现有的模块关联
        await _roleModuleRepository.DeleteAsync(rm => rm.RoleId == roleId);

        // 添加新的模块关联
        if (moduleIds != null && moduleIds.Any())
        {
            foreach (var moduleId in moduleIds.Distinct())
            {
                var roleModule = new RoleModule
                {
                    RoleId = roleId,
                    ModuleId = moduleId
                };

                await _roleModuleRepository.InsertAsync(roleModule);
            }
        }
    }

    /// <summary>
    /// 获取角色的模块列表
    /// </summary>
    public async Task<List<Guid>> GetRoleModulesAsync(Guid roleId)
    {
        var roleModules = await _roleModuleRepository.GetListAsync(rm => rm.RoleId == roleId);
        return roleModules.Select(rm => rm.ModuleId).ToList();
    }

    /// <summary>
    /// 移除角色的模块
    /// </summary>
    public async Task RemoveModuleFromRoleAsync(Guid roleId, Guid moduleId)
    {
        await EnsureRoleIsMutableAsync(roleId);

        var roleModule = await _roleModuleRepository.GetAsync(rm => 
            rm.RoleId == roleId && rm.ModuleId == moduleId);

        if (roleModule != null)
        {
            await _roleModuleRepository.DeleteAsync(rm =>
                rm.RoleId == roleId && rm.ModuleId == moduleId);
        }
    }

    /// <summary>
    /// 清空角色的所有模块
    /// </summary>
    public async Task ClearRoleModulesAsync(Guid roleId)
    {
        await EnsureRoleIsMutableAsync(roleId);
        await _roleModuleRepository.DeleteAsync(rm => rm.RoleId == roleId);
    }

    private async Task EnsureRoleIsMutableAsync(Guid roleId)
    {
        var role = await _roleRepository.GetAsync(roleId);
        if (role == null)
        {
            throw new BusinessException(SystemErrorCodes.RoleNotFound, "The role does not exist.")
                .WithData("RoleId", roleId);
        }

        if (role.IsBuiltIn)
        {
            throw new BusinessException(
                    SystemErrorCodes.BuiltInRoleProtected,
                    "The built-in role cannot be edited.")
                .WithData("RoleId", roleId);
        }
    }
}
