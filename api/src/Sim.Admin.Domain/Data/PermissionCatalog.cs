using System.Collections.Generic;
using Sim.System.Domain.Shared;
using Sim.System.Notifications;
using Sim.System.Domain.Shared.Permissions;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 系统权限清单 —— 全部可分配权限的唯一事实来源。
/// </summary>
/// <remarks>
/// 权限码统一采用 `module:resource:action` 形式（超级权限 `*:*:*` 除外），模块段与功能模块保持一致：
/// <list type="bullet">
/// <item>system —— 系统管理（用户、令牌、角色、权限、模块、路由、组织、文件、定时任务、编号、配置、字典、通知、后台任务）</item>
/// </list>
/// 服务上的 <c>[Permission]</c> 特性只能使用本清单中登记的权限码；新增接口权限时先在这里登记，
/// 再更新 `PermissionDataSeeder` 会幂等补齐数据库记录，管理端随即可见。
/// </remarks>
public static class PermissionCatalog
{
    /// <summary>权限定义：权限码、展示名称与描述。</summary>
    public sealed record PermissionDefinition(string Code, string Name, string Description);

    /// <summary>全部权限定义。</summary>
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(DataSeedConsts.SuperPermissionCode, "超级权限", "拥有系统所有权限"),

        // 系统管理 —— 用户与令牌
        new(SystemPermissions.User.Query, "查询用户", "查看用户列表与详情"),
        new(SystemPermissions.User.Create, "创建用户", "新建用户账号"),
        new(SystemPermissions.User.Update, "更新用户", "修改用户资料、角色与状态"),
        new(SystemPermissions.User.ResetPassword, "重置用户密码", "将指定用户的密码重置为系统配置的默认密码"),
        new(SystemPermissions.User.Delete, "删除用户", "删除用户账号"),
        new(SystemPermissions.Token.RevokeAll, "吊销全部令牌", "强制指定用户下线（吊销其全部刷新令牌）"),
        new(SystemPermissions.Token.Cleanup, "清理过期令牌", "清理已过期的刷新令牌记录"),

        // 系统管理 —— 角色与权限
        new(SystemPermissions.Role.Manage, "角色管理", "维护角色及其权限分配"),
        new(SystemPermissions.RoleModule.Manage, "角色模块分配", "配置角色可访问的模块"),
        new(SystemPermissions.Permission.Manage, "权限管理", "维护权限清单条目"),

        // 系统管理 —— 模块与路由
        new(SystemPermissions.ModuleConfig.Create, "创建模块", "新增功能模块配置"),
        new(SystemPermissions.ModuleConfig.Update, "更新模块", "修改功能模块配置"),
        new(SystemPermissions.ModuleConfig.Delete, "删除模块", "删除功能模块配置"),
        new(SystemPermissions.RouteConfig.Create, "创建路由", "新增菜单与路由配置"),
        new(SystemPermissions.RouteConfig.Update, "更新路由", "修改菜单与路由配置"),
        new(SystemPermissions.RouteConfig.Delete, "删除路由", "删除菜单与路由配置"),

        // 系统管理 —— 组织架构
        new(OrganizationPermissions.Manage, "组织管理", "维护组织架构、成员与数据范围"),
        new(OrganizationPermissions.View, "查看组织", "查看组织架构与成员"),

        // 系统管理 —— 文件与运维配置
        new(FileManagementPermissions.Manage, "文件管理", "上传、下载、导出、转换和处理文件"),
        new(SystemManagementPermissions.FileStorageManage, "文件存储配置", "查看和维护系统文件存储配置"),
        new(SystemManagementPermissions.ScheduleTaskManage, "定时任务管理", "查看、配置、启用和停用系统定时任务"),
        new(SystemManagementPermissions.NumberingManage, "业务编号管理", "维护业务场景的编号格式、初始值和启停状态"),
        new(SystemManagementPermissions.SystemConfigurationManage, "系统配置管理", "维护系统配置分组、配置键、配置值和启停状态"),
        new(SystemManagementPermissions.DataDictionaryManage, "数据字典管理", "维护数据字典、字典项、值类型和启停状态"),

        // 系统管理 —— 通知
        new(NotificationPermissions.Manage, "通知服务管理", "维护通知供应商、模板、规则和 Webhook Site"),
        new(NotificationPermissions.ViewDeliveries, "通知投递审计", "查看脱敏后的通知投递状态和错误"),

        // 系统管理 —— 租户
        new(TenantPermissions.Manage, "租户管理", "创建、编辑、启停和删除租户"),

        // 系统管理 —— 后台任务监控
        new("system:worker:query", "查看后台任务", "查看后台作业与监控指标"),
    ];
}
