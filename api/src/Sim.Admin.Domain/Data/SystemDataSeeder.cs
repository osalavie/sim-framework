using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Sim.System.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.ModuleConfig;
using Sim.System.Domain.RouteConfig;
using Sim.System.Domain.RouteMeta;

namespace Sim.Admin.Domain.Data;

/// <summary>
/// System module data seeder
/// </summary>
public class SystemDataSeeder
{
    private static readonly IReadOnlyDictionary<string, string> LegacyComponentPaths =
        new Dictionary<string, string>
        {
            ["/modules/user/views/Profile.vue"] = "authorization/views/user/Profile",
            ["/modules/user/views/Settings.vue"] = "authorization/views/user/Settings"
        };

    private readonly IModuleConfigRepository _moduleConfigRepository;
    private readonly IRouteConfigRepository _routeConfigRepository;
    private readonly IRouteMetaRepository _routeMetaRepository;
    private readonly IModuleRouteRepository _moduleRouteRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<SystemDataSeeder> _logger;

    public SystemDataSeeder(
        IModuleConfigRepository moduleConfigRepository,
        IRouteConfigRepository routeConfigRepository,
        IRouteMetaRepository routeMetaRepository,
        IModuleRouteRepository moduleRouteRepository,
        IHostEnvironment hostEnvironment,
        ILogger<SystemDataSeeder> logger)
    {
        _moduleConfigRepository = moduleConfigRepository;
        _routeConfigRepository = routeConfigRepository;
        _routeMetaRepository = routeMetaRepository;
        _moduleRouteRepository = moduleRouteRepository;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // Only create sample data in development environment
        if (!_hostEnvironment.IsDevelopment())
        {
            _logger.LogInformation("非开发环境，跳过系统模块示例数据创建");
            return;
        }

        var moduleIds = await SeedModulesAsync();
        await SeedRoutesAsync(moduleIds);
        await MigrateLegacyComponentPathsAsync();
    }

    /// <summary>
    /// Create sample module configurations
    /// </summary>
    private async Task<Dictionary<string, Guid>> SeedModulesAsync()
    {
        var module = await _moduleConfigRepository.GetAsync(item => item.Name == "user");
        if (module == null)
        {
            module = await _moduleConfigRepository.InsertWithReturnAsync(new ModuleConfigEntity
            {
                Name = "user",
                Description = "用户管理模块",
                Version = "1.0.0",
                Enabled = true,
                Icon = "mdi:account-outline",
                Order = 1,
                Layout = SystemConsts.LayoutTypes.Admin,
                EmptyRedirect = "/admin/user/profile"
            });
            _logger.LogInformation("创建了用户模块配置");
        }
        else if (module.EmptyRedirect == "/user/profile")
        {
            module.EmptyRedirect = "/admin/user/profile";
            await _moduleConfigRepository.UpdateAsync(module);
            _logger.LogInformation("已迁移用户模块默认跳转地址");
        }

        return new Dictionary<string, Guid> { [module.Name] = module.Id };
    }

    /// <summary>
    /// Create sample route configurations
    /// </summary>
    private async Task SeedRoutesAsync(Dictionary<string, Guid> moduleIds)
    {
        var userModuleId = moduleIds["user"];

        var userRoot = await EnsureRouteAsync(
            userModuleId, null, "UserCenter", "user", "Layout", "/admin/user/profile",
            "用户", "mdi:account-outline", false, false, false, 2);

        await EnsureRouteAsync(
            userModuleId, userRoot.Id, "UserCenterProfile", "profile",
            "authorization/views/user/Profile", null,
            "个人资料", "mdi:account-circle-outline", false, true, true, 1);

        await EnsureRouteAsync(
            userModuleId, userRoot.Id, "UserCenterSettings", "settings",
            "authorization/views/user/Settings", null,
            "用户设置", "mdi:cog-outline", false, true, true, 2);

        _logger.LogInformation("用户模块路由配置检查完成");
    }

    private async Task<RouteConfigEntity> EnsureRouteAsync(
        Guid moduleId,
        Guid? parentId,
        string name,
        string path,
        string component,
        string? redirect,
        string title,
        string icon,
        bool menu,
        bool tab,
        bool keepAlive,
        int order)
    {
        var route = await _routeConfigRepository.GetAsync(item => item.Name == name);
        if (route == null)
        {
            route = await _routeConfigRepository.InsertWithReturnAsync(new RouteConfigEntity
            {
                ParentId = parentId,
                Name = name,
                Path = path,
                Component = component,
                Redirect = redirect
            });

            await _moduleRouteRepository.InsertAsync(new ModuleRouteEntity
            {
                ModuleId = moduleId,
                RouteId = route.Id
            });
        }
        else
        {
            var needsMigration =
                (name == "UserCenter" && route.Path == "/user") ||
                (name == "UserCenter" && route.Redirect == "/user/profile");
            if (needsMigration)
            {
                route.Path = path;
                route.Redirect = redirect;
                await _routeConfigRepository.UpdateAsync(route);
                _logger.LogInformation("已迁移用户模块管理端路由: {RouteName}", name);
            }
        }

        var meta = await _routeMetaRepository.GetAsync(item => item.RouteConfigId == route.Id);
        if (meta == null)
        {
            meta = new RouteMetaEntity
            {
                RouteConfigId = route.Id,
                Title = title,
                Icon = icon,
                Menu = menu,
                Tab = tab,
                KeepAlive = keepAlive,
                Order = order,
                Module = "user"
            };
            await _routeMetaRepository.InsertAsync(meta);
        }
        else if (meta.Menu != menu)
        {
            meta.Menu = menu;
            await _routeMetaRepository.UpdateAsync(meta);
            _logger.LogInformation("已更新用户模块菜单可见性: {RouteName} -> {Menu}", name, menu);
        }

        return route;
    }

    private async Task MigrateLegacyComponentPathsAsync()
    {
        foreach (var mapping in LegacyComponentPaths)
        {
            var routes = await _routeConfigRepository.GetListAsync(
                route => route.Component == mapping.Key);

            foreach (var route in routes)
            {
                route.Component = mapping.Value;
                await _routeConfigRepository.UpdateAsync(route);
                _logger.LogInformation(
                    "已更新旧动态路由组件路径: {RouteName} -> {Component}",
                    route.Name,
                    mapping.Value);
            }
        }
    }
}
