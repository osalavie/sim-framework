using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Permissions;
using Sim.System.Notifications;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 种子路由的默认访问权限码：按路由名匹配页面读取接口所需的权限。
/// 种子只在路由元数据的权限码为空时回填，管理员在路由管理页维护的结果不会被覆盖。
/// </summary>
public static class RoutePermissionCatalog
{
    /// <summary>路由名 → 访问权限码；多个权限码逗号分隔，默认任一满足。</summary>
    public static IReadOnlyDictionary<string, string> ByRouteName { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // 授权管理
            ["Authorization"] = Join(
                SystemPermissions.User.Query,
                SystemPermissions.Role.Manage,
                SystemPermissions.ModuleConfig.Create,
                SystemPermissions.ModuleConfig.Update,
                SystemPermissions.ModuleConfig.Delete,
                SystemPermissions.RouteConfig.Create,
                SystemPermissions.RouteConfig.Update,
                SystemPermissions.RouteConfig.Delete,
                SystemPermissions.Permission.Manage),
            ["UserManagement"] = SystemPermissions.User.Query,
            ["RoleManagement"] = SystemPermissions.Role.Manage,
            ["ModuleManagement"] = Join(
                SystemPermissions.ModuleConfig.Create,
                SystemPermissions.ModuleConfig.Update,
                SystemPermissions.ModuleConfig.Delete),
            ["RouteManagement"] = Join(
                SystemPermissions.RouteConfig.Create,
                SystemPermissions.RouteConfig.Update,
                SystemPermissions.RouteConfig.Delete),
            ["PermissionManagement"] = SystemPermissions.Permission.Manage,
            ["TenantManagement"] = TenantPermissions.Manage,
            ["OrganizationManagement"] = Join(
                OrganizationPermissions.Manage,
                OrganizationPermissions.View),

            // 系统管理：根路由取全部子页面权限的并集（任一满足即可进入根菜单）
            ["System"] = Join(
                SystemManagementPermissions.FileStorageManage,
                NotificationPermissions.Manage,
                SystemManagementPermissions.ScheduleTaskManage,
                SystemManagementPermissions.NumberingManage,
                SystemManagementPermissions.SystemConfigurationManage,
                SystemManagementPermissions.DataDictionaryManage,
                TenantPermissions.Manage,
                OrganizationPermissions.Manage,
                OrganizationPermissions.View),
            ["FileStorageManagement"] = SystemManagementPermissions.FileStorageManage,
            ["NotificationManagement"] = NotificationPermissions.Manage,
            ["ScheduleTaskManagement"] = SystemManagementPermissions.ScheduleTaskManage,
            ["BusinessNumberingManagement"] = SystemManagementPermissions.NumberingManage,
            ["SystemConfigurationManagement"] = SystemManagementPermissions.SystemConfigurationManage,
            ["DataDictionaryManagement"] = SystemManagementPermissions.DataDictionaryManage,
            ["DataDictionaryDataManagement"] = SystemManagementPermissions.DataDictionaryManage,
        };

    /// <summary>
    /// 根路由历史权限并集：组织架构与租户管理迁入系统管理前。
    /// 仅当存量库中的权限码仍与历史并集完全一致时才迁移，避免覆盖管理员维护结果。
    /// </summary>
    public static IReadOnlyDictionary<string, string> LegacyByRouteName { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Authorization"] = Join(
                SystemPermissions.User.Query,
                SystemPermissions.Role.Manage,
                SystemPermissions.ModuleConfig.Create,
                SystemPermissions.ModuleConfig.Update,
                SystemPermissions.ModuleConfig.Delete,
                SystemPermissions.RouteConfig.Create,
                SystemPermissions.RouteConfig.Update,
                SystemPermissions.RouteConfig.Delete,
                SystemPermissions.Permission.Manage,
                TenantPermissions.Manage,
                OrganizationPermissions.Manage,
                OrganizationPermissions.View),
            ["System"] = Join(
                SystemManagementPermissions.FileStorageManage,
                NotificationPermissions.Manage,
                SystemManagementPermissions.ScheduleTaskManage,
                SystemManagementPermissions.NumberingManage,
                SystemManagementPermissions.SystemConfigurationManage,
                SystemManagementPermissions.DataDictionaryManage),
        };

    /// <summary>按路由名取默认权限码；没有匹配时返回 null。</summary>
    public static string? GetPermissionCode(string routeName) =>
        ByRouteName.TryGetValue(routeName, out var permissionCode) ? permissionCode : null;

    /// <summary>按路由名取历史权限并集；没有匹配时返回 null。</summary>
    public static string? GetLegacyPermissionCode(string routeName) =>
        LegacyByRouteName.TryGetValue(routeName, out var permissionCode) ? permissionCode : null;

    /// <summary>展开全部路由默认权限码，用于校验其均已登记在权限清单。</summary>
    public static IEnumerable<string> EnumeratePermissionCodes() =>
        ByRouteName.Values
            .SelectMany(codes => codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal);

    private static string Join(params string[] codes) => string.Join(',', codes);
}
