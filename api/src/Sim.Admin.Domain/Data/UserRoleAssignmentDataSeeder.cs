using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Data;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.ModuleConfig;
using Sim.System.Domain.Role;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 为内置普通用户角色补齐基础模块。
/// 自助注册默认领取该角色；管理员仍可在角色管理中增删模块，
/// 种子只追加缺失项，不回收管理员已移除的授权。
/// </summary>
public class UserRoleAssignmentDataSeeder
{
    private readonly IRoleRepository _roleRepository;
    private readonly IModuleConfigRepository _moduleConfigRepository;
    private readonly IRoleModuleRepository _roleModuleRepository;
    private readonly IEnumerable<IModuleSeedContributor> _moduleSeedContributors;
    private readonly ILogger<UserRoleAssignmentDataSeeder> _logger;

    public UserRoleAssignmentDataSeeder(
        IRoleRepository roleRepository,
        IModuleConfigRepository moduleConfigRepository,
        IRoleModuleRepository roleModuleRepository,
        IEnumerable<IModuleSeedContributor> moduleSeedContributors,
        ILogger<UserRoleAssignmentDataSeeder> logger)
    {
        _roleRepository = roleRepository;
        _moduleConfigRepository = moduleConfigRepository;
        _roleModuleRepository = roleModuleRepository;
        _moduleSeedContributors = moduleSeedContributors;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("========== 开始为普通用户角色分配基础模块 ==========");

        var userRole = await _roleRepository.GetAsync(role =>
            role.Code == RoleConst.User && !role.IsBuiltIn);
        if (userRole == null)
        {
            _logger.LogInformation("普通用户角色不存在，跳过基础模块分配");
            return;
        }

        await AssignBasicModulesAsync(userRole.Id);

        _logger.LogInformation("========== 普通用户角色基础模块分配完成 ==========");
    }

    private async Task AssignBasicModulesAsync(Guid roleId)
    {
        var basicModuleNames = _moduleSeedContributors
            .SelectMany(contributor => contributor.Modules)
            .Where(seed => seed.DefaultForUserRole)
            .Select(seed => seed.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (basicModuleNames.Count == 0)
        {
            _logger.LogInformation("没有模块声明默认授予普通用户角色，跳过模块分配");
            return;
        }

        var modules = (await _moduleConfigRepository.GetAll())
            .Where(module => basicModuleNames.Contains(module.Name))
            .ToList();
        if (modules.Count == 0)
        {
            _logger.LogInformation("基础模块尚未创建，跳过普通用户模块分配");
            return;
        }

        var existing = await _roleModuleRepository.GetListAsync(link => link.RoleId == roleId);
        var assignedIds = existing.Select(link => link.ModuleId).ToHashSet();
        var added = 0;
        foreach (var module in modules)
        {
            if (!assignedIds.Add(module.Id))
            {
                continue;
            }

            await _roleModuleRepository.InsertAsync(new RoleModule
            {
                RoleId = roleId,
                ModuleId = module.Id
            });
            added++;
        }

        _logger.LogInformation("普通用户角色新增 {AddedCount} 个基础模块", added);
    }
}
