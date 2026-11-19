using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.ModuleConfig;
using Sim.System.Domain.Permission;
using Sim.System.Domain.Role;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 管理员角色分配种子数据 - 为 Admin 角色分配超级权限和所有模块
/// </summary>
public class AdminRoleAssignmentDataSeeder
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IModuleConfigRepository _moduleConfigRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IRoleModuleRepository _roleModuleRepository;
    private readonly ILogger<AdminRoleAssignmentDataSeeder> _logger;

    public AdminRoleAssignmentDataSeeder(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IModuleConfigRepository moduleConfigRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleModuleRepository roleModuleRepository,
        ILogger<AdminRoleAssignmentDataSeeder> logger)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _moduleConfigRepository = moduleConfigRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _roleModuleRepository = roleModuleRepository;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("========== 开始为 Admin 角色分配权限和模块 ==========");

        // 获取 Admin 角色
        var adminRole = await _roleRepository.GetAsync(r => r.IsBuiltIn);
        if (adminRole == null)
        {
            _logger.LogError("内置管理员角色不存在，无法分配权限和模块");
            throw new InvalidOperationException("内置管理员角色不存在，数据种子执行失败");
        }

        _logger.LogInformation("找到 Admin 角色: {RoleId}", adminRole.Id);

        // 为 Admin 角色分配超级权限
        await AssignSuperPermissionToAdminAsync(adminRole.Id);

        // 为 Admin 角色分配所有模块
        await AssignAllModulesToAdminAsync(adminRole.Id);

        _logger.LogInformation("========== Admin 角色权限和模块分配完成 ==========");
    }

    /// <summary>
    /// 为 Admin 角色分配超级权限 "*:*:*"
    /// </summary>
    private async Task AssignSuperPermissionToAdminAsync(Guid adminRoleId)
    {
        // 获取超级权限
        var superPermission = await _permissionRepository.GetAsync(p => p.Code == DataSeedConsts.SuperPermissionCode);
        if (superPermission == null)
        {
            _logger.LogError("超级权限 {PermissionCode} 不存在，无法分配给 Admin 角色", DataSeedConsts.SuperPermissionCode);
            throw new InvalidOperationException($"超级权限 {DataSeedConsts.SuperPermissionCode} 不存在");
        }

        // 检查是否已经分配
        var existingRolePermission = await _rolePermissionRepository.GetAsync(
            rp => rp.RoleId == adminRoleId && rp.PermissionId == superPermission.Id);

        if (existingRolePermission != null)
        {
            _logger.LogDebug("超级权限已分配给 Admin 角色，跳过");
            return;
        }

        // 创建角色权限关联
        var rolePermission = new RolePermission
        {
            RoleId = adminRoleId,
            PermissionId = superPermission.Id
        };

        try
        {
            await _rolePermissionRepository.InsertAsync(rolePermission);
            _logger.LogInformation("成功为 Admin 角色分配超级权限: {PermissionCode}", DataSeedConsts.SuperPermissionCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "为 Admin 角色分配超级权限失败");
            throw;
        }
    }

    /// <summary>
    /// 为 Admin 角色分配所有模块（Admin 角色可以访问所有模块）
    /// </summary>
    private async Task AssignAllModulesToAdminAsync(Guid adminRoleId)
    {
        var allModules = await _moduleConfigRepository.GetAll();
        if (allModules == null || allModules.Count == 0)
        {
            _logger.LogWarning("系统中没有模块，跳过模块分配");
            return;
        }

        var existingRoleModules = await _roleModuleRepository.GetListAsync(rm => rm.RoleId == adminRoleId);
        var existingModuleIds = existingRoleModules?.Select(rm => rm.ModuleId).ToHashSet() ?? new HashSet<Guid>();

        var assignedCount = 0;
        foreach (var module in allModules)
        {
            if (!existingModuleIds.Contains(module.Id))
            {
                var roleModule = new RoleModule
                {
                    RoleId = adminRoleId,
                    ModuleId = module.Id
                };

                try
                {
                    await _roleModuleRepository.InsertAsync(roleModule);
                    assignedCount++;
                    _logger.LogDebug("为 Admin 角色分配模块: {ModuleName} (ID: {ModuleId})", module.Name, module.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "为 Admin 角色分配模块 {ModuleName} 失败", module.Name);
                    throw;
                }
            }
        }

        _logger.LogInformation("为 Admin 角色分配了 {AssignedCount} 个新模块，总模块数: {TotalCount}", 
            assignedCount, allModules.Count);
    }
}
