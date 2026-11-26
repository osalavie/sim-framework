using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Role;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 角色数据种子
/// 负责创建系统的基础角色：Admin, User
/// </summary>
public class RoleDataSeeder
{
    private readonly IRoleRepository _roleRepository;
    private readonly ILogger<RoleDataSeeder> _logger;

    public RoleDataSeeder(
        IRoleRepository roleRepository,
        ILogger<RoleDataSeeder> logger)
    {
        _roleRepository = roleRepository;
        _logger = logger;
    }

    /// <summary>
    /// 执行角色数据种子
    /// </summary>
    public async Task SeedAsync()
    {
        _logger.LogInformation("========== 开始创建基础角色 ==========");

        // 创建 Admin 角色
        await CreateRoleIfNotExistsAsync(
            RoleConst.Admin,
            RoleConst.Admin,
            "超级管理员，拥有所有权限",
            isBuiltIn: true
        );

        // 创建 User 角色
        await CreateRoleIfNotExistsAsync(
            RoleConst.User,
            RoleConst.User,
            "普通用户，拥有基本权限"
        );

        _logger.LogInformation("========== 基础角色创建完成 ==========");
    }

    /// <summary>
    /// 创建角色（如果不存在）
    /// 支持幂等性：如果角色已存在，则跳过创建并返回已存在的角色
    /// </summary>
    /// <param name="name">角色名称</param>
    /// <param name="code">角色代码</param>
    /// <param name="description">角色描述</param>
    /// <returns>创建或已存在的角色实体</returns>
    private async Task<RoleEntity> CreateRoleIfNotExistsAsync(
        string name,
        string code,
        string description,
        bool isBuiltIn = false)
    {
        // 检查角色是否已存在
        var existingRole = await _roleRepository.GetAsync(r => r.Code == code);
        if (existingRole != null)
        {
            if (existingRole.IsBuiltIn != isBuiltIn)
            {
                existingRole.IsBuiltIn = isBuiltIn;
                await _roleRepository.UpdateAsync(existingRole);
                _logger.LogInformation(
                    "已同步角色内置标记: {RoleName} ({Code}), IsBuiltIn={IsBuiltIn}",
                    name,
                    code,
                    isBuiltIn);
            }

            _logger.LogDebug("角色已存在，跳过创建: {RoleName} ({Code}), ID: {RoleId}", name, code, existingRole.Id);
            return existingRole;
        }

        // 创建新角色
        var role = new RoleEntity
        {
            Name = name,
            Code = code,
            Description = description,
            IsBuiltIn = isBuiltIn
        };

        await _roleRepository.InsertAsync(role);
        _logger.LogInformation("角色创建成功: {RoleName} ({Code}), ID: {RoleId}", name, code, role.Id);
        
        return role;
    }
}
