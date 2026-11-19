using System;
using System.Collections.Generic;

namespace Sim.Admin.Domain.Shared.Localization;

/// <summary>
/// Stable localization keys stored in route metadata and exposed to clients.
/// Menu keys use the ABP-style colon format <c>Menu:&lt;PascalCase&gt;</c>,
/// for example <c>Menu:ApprovalMonitor</c>.
/// </summary>
public static class MenuLocalizationKeys
{
    public const string Workspace = "Menu:Workspace";
    public const string SystemManagement = "Menu:SystemManagement";
    public const string FileStorage = "Menu:FileStorage";
    public const string ScheduleTask = "Menu:ScheduleTask";
    public const string Notifications = "Menu:Notifications";
    public const string Numbering = "Menu:Numbering";
    public const string SystemConfiguration = "Menu:SystemConfiguration";
    public const string DataDictionary = "Menu:DataDictionary";
    public const string DataDictionaryData = "Menu:DataDictionaryData";
    public const string AuthorizationManagement = "Menu:AuthorizationManagement";
    public const string UserManagement = "Menu:UserManagement";
    public const string RoleManagement = "Menu:RoleManagement";
    public const string PermissionManagement = "Menu:PermissionManagement";
    public const string ModuleManagement = "Menu:ModuleManagement";
    public const string RouteManagement = "Menu:RouteManagement";
    public const string TenantManagement = "Menu:TenantManagement";
    public const string Profile = "Menu:Profile";
    public const string OrganizationManagement = "Menu:OrganizationManagement";
    public const string Approval = "Menu:Approval";
    public const string ApprovalTodo = "Menu:ApprovalTodo";
    public const string ApprovalHandled = "Menu:ApprovalHandled";
    public const string ApprovalSubmitted = "Menu:ApprovalSubmitted";
    public const string ApprovalCc = "Menu:ApprovalCc";
    public const string ApprovalDefinition = "Menu:ApprovalDefinition";
    public const string ApprovalMonitor = "Menu:ApprovalMonitor";
    public const string OpenPlatform = "Menu:OpenPlatform";
    public const string OpenPlatformApplications = "Menu:OpenPlatformApplications";
    public const string OpenPlatformAccessLogs = "Menu:OpenPlatformAccessLogs";

    private static readonly IReadOnlyList<string> Keys = Array.AsReadOnly(
    [
        Workspace,
        SystemManagement,
        FileStorage,
        ScheduleTask,
        Notifications,
        Numbering,
        SystemConfiguration,
        DataDictionary,
        DataDictionaryData,
        AuthorizationManagement,
        UserManagement,
        RoleManagement,
        PermissionManagement,
        ModuleManagement,
        RouteManagement,
        TenantManagement,
        Profile,
        OrganizationManagement,
        Approval,
        ApprovalTodo,
        ApprovalHandled,
        ApprovalSubmitted,
        ApprovalCc,
        ApprovalDefinition,
        ApprovalMonitor,
        OpenPlatform,
        OpenPlatformApplications,
        OpenPlatformAccessLogs
    ]);

    private static readonly IReadOnlyDictionary<string, string[]> LegacyTitles =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [SystemManagement] = ["系统管理", "System Management"],
            [FileStorage] = ["文件存储", "File Storage"],
            [ScheduleTask] = ["定时任务", "Scheduled Tasks"],
            [Notifications] = ["通知服务", "Notifications"],
            [Numbering] = ["业务编号", "Business numbering"],
            [SystemConfiguration] = ["系统配置", "System Configuration"],
            [DataDictionary] = ["数据字典", "Data Dictionary"],
            [DataDictionaryData] = ["字典数据", "Dictionary Data"],
            [AuthorizationManagement] = ["授权管理", "Authorization Management"],
            [UserManagement] = ["用户管理", "User Management"],
            [RoleManagement] = ["角色管理", "Role Management"],
            [PermissionManagement] = ["权限管理", "Permission Management"],
            [ModuleManagement] = ["模块管理", "Module Management"],
            [RouteManagement] = ["路由管理", "Route Management"],
            [TenantManagement] = ["租户管理", "Tenant Management"],
            [Profile] = ["个人信息", "Profile"],
            [OrganizationManagement] = ["组织架构", "Organization", "Organization Management"],
            [Approval] = ["审批", "Approval"],
            [ApprovalTodo] = ["我的待办", "My To-do"],
            [ApprovalHandled] = ["我的已办", "My Handled"],
            [ApprovalSubmitted] = ["我发起的", "Submitted by Me"],
            [ApprovalCc] = ["抄送我的", "Cc to Me"],
            [ApprovalDefinition] = ["流程定义", "Flow Definitions"],
            [ApprovalMonitor] = ["实例监控", "Instance Monitor"],
            [OpenPlatform] = ["开放平台", "Open Platform"],
            [OpenPlatformApplications] = ["应用管理", "Applications"],
            [OpenPlatformAccessLogs] = ["访问日志", "Access Logs"]
        };

    public static IReadOnlyList<string> ClientKeys => Keys;

    public static bool IsLegacyTitle(string? title, string localizationKey)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(localizationKey))
        {
            return false;
        }

        if (LegacyTitles.TryGetValue(localizationKey, out var legacyTitles) &&
            Array.Exists(legacyTitles, legacy => string.Equals(legacy, title, StringComparison.Ordinal)))
        {
            return true;
        }

        // 兼容历史 dot 分隔键（menu.approvalMonitor）：种子重复执行时迁移为 Menu:ApprovalMonitor。
        var legacyDotKey = ToLegacyDotKey(localizationKey);
        return legacyDotKey is not null &&
               string.Equals(legacyDotKey, title, StringComparison.Ordinal);
    }

    private static string? ToLegacyDotKey(string localizationKey)
    {
        const string prefix = "Menu:";
        if (!localizationKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = localizationKey[prefix.Length..];
        return name.Length == 0
            ? null
            : $"menu.{char.ToLowerInvariant(name[0])}{name[1..]}";
    }
}
