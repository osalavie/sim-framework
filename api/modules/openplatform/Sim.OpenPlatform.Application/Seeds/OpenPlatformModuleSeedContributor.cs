using Sim.Framework.Domain.Shared.Data;
using Sim.OpenPlatform.Domain.Shared;

namespace Sim.OpenPlatform.Application.Seeds;

public sealed class OpenPlatformModuleSeedContributor : IModuleSeedContributor
{
    public IReadOnlyList<ModuleSeed> Modules => ModuleSeeds;
    public IReadOnlyList<PermissionSeed> Permissions => PermissionSeeds;
    public IReadOnlyList<RouteSeed> Routes => RouteSeeds;

    public static IReadOnlyList<ModuleSeed> ModuleSeeds { get; } =
    [
        new("openplatform", "开放平台模块，包含第三方应用接入、授权范围与令牌签发管理",
            "carbon:Api", 3000, "admin", "/admin/openplatform/applications")
    ];

    public static IReadOnlyList<PermissionSeed> PermissionSeeds { get; } =
    [
        new(OpenPlatformPermissions.Manage, "开放平台管理", "维护第三方应用及其授权范围"),
        new(OpenPlatformPermissions.ViewAccessLogs, "查看开放平台访问日志", "查询令牌签发审计记录")
    ];

    public static IReadOnlyList<RouteSeed> RouteSeeds { get; } =
    [
        new("openplatform", "OpenPlatform", "openplatform", "Layout", null,
            "Menu:OpenPlatform", "carbon:Api", 3000, true, false, false,
            PermissionCode: $"{OpenPlatformPermissions.Manage},{OpenPlatformPermissions.ViewAccessLogs}",
            Redirect: "/admin/openplatform/applications"),
        new("openplatform", "OpenPlatformApplications", "applications",
            "openplatform/views/ApplicationsView", "OpenPlatform", "Menu:OpenPlatformApplications",
            "carbon:Application", 1, true, true, true,
            PermissionCode: OpenPlatformPermissions.Manage),
        new("openplatform", "OpenPlatformAccessLogs", "access-logs",
            "openplatform/views/AccessLogsView", "OpenPlatform", "Menu:OpenPlatformAccessLogs",
            "carbon:List", 2, true, true, true,
            PermissionCode: OpenPlatformPermissions.ViewAccessLogs)
    ];
}
