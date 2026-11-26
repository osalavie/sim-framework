using Microsoft.Extensions.Logging;
using Sim.Admin.Domain.Data;
using Sim.Framework.Domain.Shared.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Sim.Admin.Domain;

/// <summary>
/// 数据种子主入口
/// 按照正确的依赖顺序执行各个数据种子，并向初始化进度接口上报每一步：
/// 1. 创建默认租户
/// 2. 执行系统模块数据种子
/// 3. 创建基础角色
/// 4. 创建基础用户并分配角色
/// 5. 创建功能模块
/// 6. 创建路由配置（菜单）
/// 7. 创建权限
/// 8. 写入系统配置参数
/// 9. 创建根组织节点
/// 10. 为 Admin 角色分配权限和模块
/// 11. 为普通用户角色补齐基础权限与模块
/// 模块自身的附加种子（如审批编号定义）由各模块的种子贡献者另行执行。
/// </summary>
public class DataSeederContributor : IDataSeedContributor
{
    /// <summary>
    /// 种子步骤顺序。Key 与前端进度展示共用，Name 用于日志。
    /// </summary>
    public static readonly IReadOnlyList<DataSeedStep> SeedSteps =
    [
        new("tenant", "创建默认租户"),
        new("system-modules", "初始化系统模块"),
        new("roles", "创建基础角色"),
        new("users", "创建基础用户"),
        new("modules", "创建功能模块"),
        new("routes", "创建路由与菜单"),
        new("permissions", "创建权限"),
        new("configurations", "写入系统配置"),
        new("organization", "创建根组织节点"),
        new("admin-role", "配置管理员权限"),
        new("basic-role", "配置普通用户基础权限")
    ];

    private readonly TenantDataSeeder _tenantDataSeeder;
    private readonly RoleDataSeeder _roleDataSeeder;
    private readonly UserDataSeeder _userDataSeeder;
    private readonly ModuleDataSeeder _moduleDataSeeder;
    private readonly RouteDataSeeder _routeDataSeeder;
    private readonly PermissionDataSeeder _permissionDataSeeder;
    private readonly SystemConfigurationDataSeeder _systemConfigurationDataSeeder;
    private readonly AdminRoleAssignmentDataSeeder _adminRoleAssignmentDataSeeder;
    private readonly UserRoleAssignmentDataSeeder _userRoleAssignmentDataSeeder;
    private readonly SystemDataSeeder _systemDataSeeder;
    private readonly OrganizationDataSeeder _organizationDataSeeder;
    private readonly ILogger<DataSeederContributor> _logger;

    public DataSeederContributor(
        TenantDataSeeder tenantDataSeeder,
        RoleDataSeeder roleDataSeeder,
        UserDataSeeder userDataSeeder,
        ModuleDataSeeder moduleDataSeeder,
        RouteDataSeeder routeDataSeeder,
        PermissionDataSeeder permissionDataSeeder,
        SystemConfigurationDataSeeder systemConfigurationDataSeeder,
        AdminRoleAssignmentDataSeeder adminRoleAssignmentDataSeeder,
        UserRoleAssignmentDataSeeder userRoleAssignmentDataSeeder,
        SystemDataSeeder systemDataSeeder,
        OrganizationDataSeeder organizationDataSeeder,
        ILogger<DataSeederContributor> logger)
    {
        _tenantDataSeeder = tenantDataSeeder;
        _roleDataSeeder = roleDataSeeder;
        _userDataSeeder = userDataSeeder;
        _moduleDataSeeder = moduleDataSeeder;
        _routeDataSeeder = routeDataSeeder;
        _permissionDataSeeder = permissionDataSeeder;
        _systemConfigurationDataSeeder = systemConfigurationDataSeeder;
        _adminRoleAssignmentDataSeeder = adminRoleAssignmentDataSeeder;
        _userRoleAssignmentDataSeeder = userRoleAssignmentDataSeeder;
        _systemDataSeeder = systemDataSeeder;
        _organizationDataSeeder = organizationDataSeeder;
        _logger = logger;
    }

    public IReadOnlyList<DataSeedStep> Steps => SeedSteps;

    /// <summary>
    /// 执行数据种子
    /// </summary>
    public async Task SeedAsync(Action<DataSeedStep>? onStep = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var statistics = new DataSeedStatistics();

        try
        {
            _logger.LogInformation("========================================");
            _logger.LogInformation("开始执行应用数据种子初始化");
            _logger.LogInformation("========================================");

            // 1. 创建默认租户（必须在用户/角色之前）
            await RunStepAsync(0, onStep, () => _tenantDataSeeder.SeedAsync());
            statistics.TenantsCreated = 1;

            // 2. 系统模块数据种子
            await RunStepAsync(1, onStep, () => _systemDataSeeder.SeedAsync());
            statistics.SystemModulesSeeded = true;

            // 3. 创建基础角色（Admin, User）
            await RunStepAsync(2, onStep, () => _roleDataSeeder.SeedAsync());
            statistics.RolesCreated = 2;
            _logger.LogInformation("✓ 基础角色创建完成");

            // 4. 创建基础用户并分配角色（admin）
            await RunStepAsync(3, onStep, () => _userDataSeeder.SeedAsync());
            statistics.UsersCreated = 1;
            _logger.LogInformation("✓ 基础用户创建完成");

            // 5. 创建功能模块（system-management, authorization-management, approval）
            await RunStepAsync(4, onStep, () => _moduleDataSeeder.SeedAsync());
            statistics.ModulesCreated = 3;
            _logger.LogInformation("✓ 功能模块创建完成");

            // 6. 创建路由配置（菜单）
            await RunStepAsync(5, onStep, () => _routeDataSeeder.SeedAsync());
            statistics.RoutesCreated = 15;
            _logger.LogInformation("✓ 路由配置创建完成");

            // 7. 创建权限（超级权限 *:*:*）
            await RunStepAsync(6, onStep, () => _permissionDataSeeder.SeedAsync());
            statistics.PermissionsCreated = 1;
            _logger.LogInformation("✓ 权限创建完成");

            // 8. 创建系统配置参数
            await RunStepAsync(7, onStep, () => _systemConfigurationDataSeeder.SeedAsync());
            statistics.SystemConfigurationItemsSeeded = true;

            // 8. 创建根组织节点（Sim）
            await RunStepAsync(8, onStep, () => _organizationDataSeeder.SeedAsync());
            statistics.OrganizationSeeded = true;

            // 9. 为 Admin 角色分配权限和模块
            await RunStepAsync(9, onStep, () => _adminRoleAssignmentDataSeeder.SeedAsync());
            statistics.AdminRoleConfigured = true;
            _logger.LogInformation("✓ Admin 角色配置完成");

            // 10. 为普通用户角色补齐基础权限与模块（自助注册默认角色）
            await RunStepAsync(10, onStep, () => _userRoleAssignmentDataSeeder.SeedAsync());
            _logger.LogInformation("✓ 普通用户角色基础权限配置完成");

            stopwatch.Stop();

            // 输出执行统计信息
            _logger.LogInformation("========================================");
            _logger.LogInformation("应用数据种子初始化完成");
            _logger.LogInformation("========================================");
            _logger.LogInformation("执行统计:");
            _logger.LogInformation("  - 租户创建: {TenantsCreated} 个", statistics.TenantsCreated);
            _logger.LogInformation("  - 角色创建: {RolesCreated} 个", statistics.RolesCreated);
            _logger.LogInformation("  - 用户创建: {UsersCreated} 个", statistics.UsersCreated);
            _logger.LogInformation("  - 模块创建: {ModulesCreated} 个", statistics.ModulesCreated);
            _logger.LogInformation("  - 路由创建: {RoutesCreated} 个", statistics.RoutesCreated);
            _logger.LogInformation("  - 权限创建: {PermissionsCreated} 个", statistics.PermissionsCreated);
            _logger.LogInformation("  - Admin 角色配置: {AdminConfigured}", statistics.AdminRoleConfigured ? "是" : "否");
            _logger.LogInformation("  - 总耗时: {ElapsedMs} ms", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("========================================");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "数据种子执行失败: {Message}，耗时: {ElapsedMs} ms", 
                ex.Message, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task RunStepAsync(
        int index,
        Action<DataSeedStep>? onStep,
        Func<Task> action)
    {
        var step = SeedSteps[index];
        _logger.LogInformation(
            "步骤 {Index}/{Total}: {Name}...",
            index + 1,
            SeedSteps.Count,
            step.Name);
        onStep?.Invoke(step);
        await action();
    }

    /// <summary>
    /// 数据种子执行统计信息
    /// </summary>
    private class DataSeedStatistics
    {
        public int TenantsCreated { get; set; }
        public bool SystemModulesSeeded { get; set; }
        public int RolesCreated { get; set; }
        public int UsersCreated { get; set; }
        public int ModulesCreated { get; set; }
        public int RoutesCreated { get; set; }
        public int PermissionsCreated { get; set; }
        public bool SystemConfigurationItemsSeeded { get; set; }
        public bool OrganizationSeeded { get; set; }
        public bool AdminRoleConfigured { get; set; }
    }
}
