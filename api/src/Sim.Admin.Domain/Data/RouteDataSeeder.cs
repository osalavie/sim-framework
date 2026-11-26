using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sim.Admin.Domain.Shared.Localization;
using Sim.Framework.Domain.Shared.Data;
using Sim.System.Domain.ModuleConfig;
using Sim.System.Domain.RouteConfig;
using Sim.System.Domain.RouteMeta;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// 路由数据种子
/// 负责为模块创建路由配置（菜单）
/// </summary>
public class RouteDataSeeder
{
    private readonly IModuleConfigRepository _moduleConfigRepository;
    private readonly IRouteConfigRepository _routeConfigRepository;
    private readonly IRouteMetaRepository _routeMetaRepository;
    private readonly IModuleRouteRepository _moduleRouteRepository;
    private readonly IEnumerable<IModuleSeedContributor> _moduleSeedContributors;
    private readonly ILogger<RouteDataSeeder> _logger;

    public RouteDataSeeder(
        IModuleConfigRepository moduleConfigRepository,
        IRouteConfigRepository routeConfigRepository,
        IRouteMetaRepository routeMetaRepository,
        IModuleRouteRepository moduleRouteRepository,
        IEnumerable<IModuleSeedContributor> moduleSeedContributors,
        ILogger<RouteDataSeeder> logger)
    {
        _moduleConfigRepository = moduleConfigRepository;
        _routeConfigRepository = routeConfigRepository;
        _routeMetaRepository = routeMetaRepository;
        _moduleRouteRepository = moduleRouteRepository;
        _moduleSeedContributors = moduleSeedContributors;
        _logger = logger;
    }

    /// <summary>
    /// 执行路由数据种子
    /// </summary>
    public async Task SeedAsync()
    {
        _logger.LogInformation("========== 开始创建路由配置（菜单） ==========");

        // 获取系统管理模块
        var systemModule = await _moduleConfigRepository.GetAsync(m => m.Name == "system-management");
        if (systemModule != null)
        {
            await RemoveLegacyDictionaryRouteAsync();
            await SeedSystemManagementRoutesAsync(systemModule.Id);
        }
        else
        {
            _logger.LogWarning("系统管理模块不存在，跳过路由创建");
        }

        // 获取授权管理模块
        var authModule = await _moduleConfigRepository.GetAsync(m => m.Name == "authorization-management");
        if (authModule != null)
        {
            await SeedAuthorizationManagementRoutesAsync(authModule.Id);
        }
        else
        {
            _logger.LogWarning("授权管理模块不存在，跳过路由创建");
        }

        // 组织架构与租户管理迁入系统管理后，根路由权限并集需要同步迁移。
        await MigrateRootPermissionCodesAsync();

        // 各功能模块自描述的路由
        foreach (var contributor in _moduleSeedContributors)
        {
            await SeedContributedRoutesAsync(contributor.Routes);
        }

        _logger.LogInformation("========== 路由配置（菜单）创建完成 ==========");
    }

    private async Task SeedContributedRoutesAsync(IReadOnlyList<RouteSeed> routes)
    {
        if (routes.Count == 0)
        {
            return;
        }

        var moduleIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var routeIds = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var routeSeed in routes
                     .OrderBy(route => route.ParentName == null ? 0 : 1)
                     .ThenBy(route => route.Order)
                     .ThenBy(route => route.Name, StringComparer.Ordinal))
        {
            if (!moduleIds.TryGetValue(routeSeed.ModuleName, out var moduleId))
            {
                var module = await _moduleConfigRepository.GetAsync(m => m.Name == routeSeed.ModuleName);
                if (module == null)
                {
                    _logger.LogWarning("路由所属模块不存在，跳过: {ModuleName}/{RouteName}", routeSeed.ModuleName, routeSeed.Name);
                    continue;
                }

                moduleId = module.Id;
                moduleIds[routeSeed.ModuleName] = moduleId;
            }

            Guid? parentId = null;
            if (!string.IsNullOrWhiteSpace(routeSeed.ParentName))
            {
                if (!routeIds.TryGetValue(routeSeed.ParentName, out var resolvedParentId))
                {
                    var parent = await _routeConfigRepository.GetAsync(route => route.Name == routeSeed.ParentName);
                    if (parent == null)
                    {
                        _logger.LogWarning(
                            "路由父节点不存在，跳过: {ModuleName}/{RouteName} -> {ParentName}",
                            routeSeed.ModuleName,
                            routeSeed.Name,
                            routeSeed.ParentName);
                        continue;
                    }

                    resolvedParentId = parent.Id;
                    routeIds[routeSeed.ParentName] = resolvedParentId;
                }

                parentId = resolvedParentId;
            }

            var route = await CreateRouteIfNotExistsAsync(
                moduleId: moduleId,
                name: routeSeed.Name,
                path: routeSeed.Path,
                component: routeSeed.Component,
                parentId: parentId,
                title: routeSeed.Title,
                icon: routeSeed.Icon,
                order: routeSeed.Order,
                menu: routeSeed.Menu,
                tab: routeSeed.Tab,
                keepAlive: routeSeed.KeepAlive,
                permissionCode: routeSeed.PermissionCode,
                redirect: routeSeed.Redirect);

            if (route != null)
            {
                routeIds[routeSeed.Name] = route.Id;
            }
        }
    }

    /// <summary>
    /// 为系统管理模块创建路由
    /// 创建一条主路由：/system
    /// </summary>
    private async Task RemoveLegacyDictionaryRouteAsync()
    {
        var legacyRoutes = await _routeConfigRepository.GetListAsync(route =>
            route.Name == "DictionaryManagement");
        foreach (var legacyRoute in legacyRoutes)
        {
            await _moduleRouteRepository.DeleteAsync(link => link.RouteId == legacyRoute.Id);
            await _routeMetaRepository.DeleteAsync(meta => meta.RouteConfigId == legacyRoute.Id);
            await _routeConfigRepository.DeleteAsync(route => route.Id == legacyRoute.Id);
            _logger.LogInformation("已移除旧分组参数路由: {RouteId}", legacyRoute.Id);
        }
    }

    /// <summary>
    /// 将历史遗留的小写系统管理根路由名 "system" 迁移为 PascalCase "System"，
    /// 与其他模块根路由命名保持一致。
    /// </summary>
    private async Task RenameLegacySystemRouteAsync()
    {
        var legacyRoute = await _routeConfigRepository.GetAsync(route =>
            route.Name == "system" && route.ParentId == null);
        if (legacyRoute == null)
        {
            return;
        }

        legacyRoute.Name = "System";
        await _routeConfigRepository.UpdateAsync(legacyRoute);
        _logger.LogInformation("已迁移系统管理根路由名: system -> System");
    }

    private async Task SeedSystemManagementRoutesAsync(Guid moduleId)
    {
        _logger.LogInformation("开始创建系统管理模块路由");

        await RenameLegacySystemRouteAsync();

        // 创建系统管理主路由
        var systemRoute = await CreateRouteIfNotExistsAsync(
            moduleId: moduleId,
            name: "System",
            path: "system",
            component: "Layout",
            parentId: null,
            title: MenuLocalizationKeys.SystemManagement,
            icon: "carbon:Settings",
            order: 1000,
            menu: true,
            tab: true,
            keepAlive: true
        );

        if (systemRoute != null)
        {
            await CreateRouteIfNotExistsAsync(
                moduleId,
                "FileStorageManagement",
                "file-storage",
                "systemManagement/views/storage/index",
                systemRoute.Id,
                MenuLocalizationKeys.FileStorage,
                "carbon:FileStorage",
                1,
                true,
                true,
                true);
            await CreateRouteIfNotExistsAsync(
                moduleId,
                "NotificationManagement",
                "notifications",
                "systemManagement/views/notifications/index",
                systemRoute.Id,
                MenuLocalizationKeys.Notifications,
                "carbon:Notification",
                3,
                true,
                true,
                true);
            await CreateRouteIfNotExistsAsync(
                moduleId,
                "ScheduleTaskManagement",
                "schedule-task",
                "systemManagement/views/schedule/index",
                systemRoute.Id,
                MenuLocalizationKeys.ScheduleTask,
                "carbon:Time",
                2,
                true,
                true,
                true);
            await CreateRouteIfNotExistsAsync(
                moduleId,
                "BusinessNumberingManagement",
                "numbering",
                "systemManagement/views/numbering/index",
                systemRoute.Id,
                MenuLocalizationKeys.Numbering,
                "carbon:Number1",
                4,
                true,
                true,
                true);
            await CreateRouteIfNotExistsAsync(
                moduleId,
                "SystemConfigurationManagement",
                "configuration",
                "systemManagement/views/configuration/index",
                systemRoute.Id,
                MenuLocalizationKeys.SystemConfiguration,
                "carbon:SettingsCheck",
                5,
                true,
                true,
                true);
            await CreateRouteIfNotExistsAsync(
                moduleId,
                "DataDictionaryManagement",
                "data-dictionary",
                "systemManagement/views/data-dictionary/index",
                systemRoute.Id,
                MenuLocalizationKeys.DataDictionary,
                "carbon:DataShare",
                6,
                true,
                true,
                true);
            await CreateRouteIfNotExistsAsync(
                moduleId,
                "DataDictionaryDataManagement",
                "data-dictionary-data/:dictionaryId",
                "systemManagement/views/data-dictionary-data/index",
                systemRoute.Id,
                MenuLocalizationKeys.DataDictionaryData,
                "carbon:ListBoxes",
                7,
                false,
                true,
                false);
            await CreateRouteIfNotExistsAsync(
                moduleId,
                "TenantManagement",
                "tenant",
                "authorization/views/tenant/index",
                systemRoute.Id,
                MenuLocalizationKeys.TenantManagement,
                "ionicons5:BusinessOutline",
                8,
                true,
                true,
                true);
            await CreateRouteIfNotExistsAsync(
                moduleId,
                "OrganizationManagement",
                "organization",
                "authorization/views/organization/index",
                systemRoute.Id,
                MenuLocalizationKeys.OrganizationManagement,
                "carbon:TreeView",
                9,
                true,
                true,
                true);

            await MigrateAuthorizationRoutesToSystemAsync(moduleId, systemRoute.Id);
        }

        _logger.LogInformation("系统管理模块路由创建完成");
    }

    /// <summary>
    /// 组织架构与租户管理历史上位于授权管理菜单，迁移到系统管理根菜单下：
    /// 修正父级、旧默认排序与模块关联；管理员自定义的排序保持不动。
    /// </summary>
    private async Task MigrateAuthorizationRoutesToSystemAsync(Guid systemModuleId, Guid systemRouteId)
    {
        await MoveRouteToSystemAsync(systemModuleId, systemRouteId, "TenantManagement", legacyOrder: 6, order: 8);
        await MoveRouteToSystemAsync(systemModuleId, systemRouteId, "OrganizationManagement", legacyOrder: 7, order: 9);
    }

    private async Task MoveRouteToSystemAsync(
        Guid systemModuleId,
        Guid systemRouteId,
        string routeName,
        int legacyOrder,
        int order)
    {
        var route = await _routeConfigRepository.GetAsync(item => item.Name == routeName);
        if (route == null)
        {
            return;
        }

        if (route.ParentId != systemRouteId)
        {
            route.ParentId = systemRouteId;
            await _routeConfigRepository.UpdateAsync(route);
            _logger.LogInformation("已将路由迁移到系统管理: {RouteName}", routeName);
        }

        var meta = await _routeMetaRepository.GetAsync(item => item.RouteConfigId == route.Id);
        if (meta != null && meta.Order == legacyOrder)
        {
            meta.Order = order;
            await _routeMetaRepository.UpdateAsync(meta);
        }

        var staleLinks = await _moduleRouteRepository.GetListAsync(
            link => link.RouteId == route.Id && link.ModuleId != systemModuleId);
        foreach (var staleLink in staleLinks)
        {
            await _moduleRouteRepository.DeleteAsync(link => link.Id == staleLink.Id);
            _logger.LogInformation("已解除路由的旧模块关联: {RouteName}", routeName);
        }
    }

    /// <summary>
    /// 根路由权限并集随子菜单调整而变化；仅在存量值仍等于历史并集时替换，
    /// 管理员在路由管理页维护过的权限码保持不动。
    /// </summary>
    private async Task MigrateRootPermissionCodesAsync()
    {
        await MigrateRootPermissionCodeAsync("System");
        await MigrateRootPermissionCodeAsync("Authorization");
    }

    private async Task MigrateRootPermissionCodeAsync(string routeName)
    {
        var legacyCode = RoutePermissionCatalog.GetLegacyPermissionCode(routeName);
        var currentCode = RoutePermissionCatalog.GetPermissionCode(routeName);
        if (string.IsNullOrWhiteSpace(legacyCode) || string.IsNullOrWhiteSpace(currentCode))
        {
            return;
        }

        var route = await _routeConfigRepository.GetAsync(item => item.Name == routeName);
        if (route == null)
        {
            return;
        }

        var meta = await _routeMetaRepository.GetAsync(item => item.RouteConfigId == route.Id);
        if (meta == null || string.IsNullOrWhiteSpace(meta.PermissionCode) ||
            !HasSamePermissionSet(meta.PermissionCode, legacyCode))
        {
            return;
        }

        meta.PermissionCode = currentCode;
        await _routeMetaRepository.UpdateAsync(meta);
        _logger.LogInformation("已更新根路由权限并集: {RouteName}", routeName);
    }

    private static bool HasSamePermissionSet(string left, string right)
    {
        static HashSet<string> Split(string value) => value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        return Split(left).SetEquals(Split(right));
    }

    /// <summary>
    /// 为授权管理模块创建路由
    /// 创建主路由和五个子路由：
    /// - /authorization (主路由)
    ///   - /authorization/user (用户管理)
    ///   - /authorization/role (角色管理)
    ///   - /authorization/module (模块管理)
    ///   - /authorization/route (路由管理)
    ///   - /authorization/permission (权限管理)
    /// </summary>
    /// <param name="moduleId">模块ID</param>
    private async Task SeedAuthorizationManagementRoutesAsync(Guid moduleId)
    {
        _logger.LogInformation("开始创建授权管理模块路由");

        // 创建授权管理主路由
        var authParentRoute = await CreateRouteIfNotExistsAsync(
            moduleId: moduleId,
            name: "Authorization",
            path: "authorization",
            component: "Layout",
            parentId: null,
            title: MenuLocalizationKeys.AuthorizationManagement,
            icon: "carbon:GroupSecurity",
            order: 2000,
            menu: true,
            tab: true,
            keepAlive: true
        );

        if (authParentRoute != null)
        {
            // 创建用户管理子路由
            await CreateRouteIfNotExistsAsync(
                moduleId: moduleId,
                name: "UserManagement",
                path: "user",
                component: "authorization/views/user/index",
                parentId: authParentRoute.Id,
                title: MenuLocalizationKeys.UserManagement,
                icon: "carbon:User",
                order: 1,
                menu: true,
                tab: true,
                keepAlive: true
            );

            // 创建角色管理子路由
            await CreateRouteIfNotExistsAsync(
                moduleId: moduleId,
                name: "RoleManagement",
                path: "role",
                component: "authorization/views/role/index",
                parentId: authParentRoute.Id,
                title: MenuLocalizationKeys.RoleManagement,
                icon: "carbon:UserRole",
                order: 2,
                menu: true,
                tab: true,
                keepAlive: true
            );

            // 创建模块管理子路由
            await CreateRouteIfNotExistsAsync(
                moduleId: moduleId,
                name: "ModuleManagement",
                path: "module",
                component: "authorization/views/module/index",
                parentId: authParentRoute.Id,
                title: MenuLocalizationKeys.ModuleManagement,
                icon: "carbon:Application",
                order: 3,
                menu: true,
                tab: true,
                keepAlive: true
            );

            // 创建路由管理子路由
            await CreateRouteIfNotExistsAsync(
                moduleId: moduleId,
                name: "RouteManagement",
                path: "route",
                component: "authorization/views/route/index",
                parentId: authParentRoute.Id,
                title: MenuLocalizationKeys.RouteManagement,
                icon: "carbon:Menu",
                order: 4,
                menu: true,
                tab: true,
                keepAlive: true
            );

            // 创建权限管理子路由
            await CreateRouteIfNotExistsAsync(
                moduleId: moduleId,
                name: "PermissionManagement",
                path: "permission",
                component: "authorization/views/permission/index",
                parentId: authParentRoute.Id,
                title: MenuLocalizationKeys.PermissionManagement,
                icon: "carbon:TwoFactorAuthentication",
                order: 5,
                menu: true,
                tab: true,
                keepAlive: true
            );
        }

        _logger.LogInformation("授权管理模块路由创建完成");
    }

    /// <summary>
    /// 创建路由配置（如果不存在）
    /// 支持幂等性：如果路由已存在，则跳过创建并返回已存在的路由
    /// </summary>
    /// <param name="moduleId">模块ID</param>
    /// <param name="name">路由名称</param>
    /// <param name="path">路由路径</param>
    /// <param name="component">组件路径</param>
    /// <param name="parentId">父路由ID</param>
    /// <param name="title">路由标题</param>
    /// <param name="icon">路由图标</param>
    /// <param name="order">排序值</param>
    /// <param name="menu">是否显示在菜单</param>
    /// <param name="tab">是否显示在标签页</param>
    /// <param name="keepAlive">是否保持活动状态</param>
    /// <returns>创建或已存在的路由实体</returns>
    private async Task<RouteConfigEntity?> CreateRouteIfNotExistsAsync(
        Guid moduleId,
        string name,
        string path,
        string component,
        Guid? parentId,
        string title,
        string? icon,
        int order,
        bool menu,
        bool tab,
        bool keepAlive,
        string? permissionCode = null,
        string? redirect = null)
    {
        permissionCode ??= RoutePermissionCatalog.GetPermissionCode(name);

        // 检查路由是否已存在（路由名全局唯一）
        var existingRoute = await _routeConfigRepository.GetAsync(r => r.Name == name);
        
        if (existingRoute != null)
        {
            if (parentId == null && existingRoute.Path == $"/{path}")
            {
                existingRoute.Path = path;
                await _routeConfigRepository.UpdateAsync(existingRoute);
                _logger.LogInformation("已迁移管理端模块根路由: {RouteName}", name);
            }

            if (!string.IsNullOrWhiteSpace(redirect) && existingRoute.Redirect != redirect)
            {
                existingRoute.Redirect = redirect;
                await _routeConfigRepository.UpdateAsync(existingRoute);
                _logger.LogInformation("已设置模块默认跳转: {RouteName} -> {Redirect}", name, redirect);
            }

            var existingMeta = await _routeMetaRepository.GetAsync(meta =>
                meta.RouteConfigId == existingRoute.Id);
            if (existingMeta == null)
            {
                await _routeMetaRepository.InsertAsync(new RouteMetaEntity
                {
                    RouteConfigId = existingRoute.Id,
                    Title = title,
                    Icon = icon,
                    Menu = menu,
                    Tab = tab,
                    KeepAlive = keepAlive,
                    Order = order,
                    PermissionCode = permissionCode
                });
            }
            else
            {
                var metaChanged = false;

                if (MenuLocalizationKeys.IsLegacyTitle(existingMeta.Title, title))
                {
                    existingMeta.Title = title;
                    metaChanged = true;
                    _logger.LogInformation(
                        "已迁移路由本地化键: {RouteName} -> {Title}",
                        name,
                        title);
                }

                // 只补空值，避免覆盖管理员在路由管理页维护的权限码
                if (!string.IsNullOrWhiteSpace(permissionCode) &&
                    string.IsNullOrWhiteSpace(existingMeta.PermissionCode))
                {
                    existingMeta.PermissionCode = permissionCode;
                    metaChanged = true;
                }

                if (metaChanged)
                {
                    await _routeMetaRepository.UpdateAsync(existingMeta);
                }
            }

            // 存量库可能只有路由而没有模块关联（关联表引入前创建），每次种子都幂等回填。
            await EnsureModuleRouteAsync(moduleId, existingRoute.Id);

            _logger.LogDebug("路由已存在，跳过创建: {RouteName}", name);
            return existingRoute;
        }

        // 创建新路由
        var route = new RouteConfigEntity
        {
            ParentId = parentId,
            Name = name,
            Path = path,
            Component = component,
            Redirect = redirect
        };

        await _routeConfigRepository.InsertAsync(route);
        await EnsureModuleRouteAsync(moduleId, route.Id);

        // 创建路由元数据
        var meta = new RouteMetaEntity
        {
            RouteConfigId = route.Id,
            Title = title,
            Icon = icon,
            Menu = menu,
            Tab = tab,
            KeepAlive = keepAlive,
            Order = order,
            PermissionCode = permissionCode
        };

        await _routeMetaRepository.InsertAsync(meta);

        _logger.LogInformation("路由创建成功: {RouteName} - {Title}, ID: {RouteId}", 
            name, title, route.Id);
        
        return route;
    }

    /// <summary>
    /// 确保模块与路由的关联存在（多对多，幂等）。
    /// </summary>
    private async Task EnsureModuleRouteAsync(Guid moduleId, Guid routeId)
    {
        var link = await _moduleRouteRepository.GetAsync(
            item => item.ModuleId == moduleId && item.RouteId == routeId);
        if (link == null)
        {
            await _moduleRouteRepository.InsertAsync(new ModuleRouteEntity
            {
                ModuleId = moduleId,
                RouteId = routeId
            });
        }
    }
}
