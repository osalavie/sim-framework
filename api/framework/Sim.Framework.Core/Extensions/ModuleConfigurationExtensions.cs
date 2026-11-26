using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sim.Framework.Application.Contracts.IServices;
using Sim.Framework.Core.Configuration;
using Sim.Framework.Core.Conventions;
using Sim.Framework.Core.Filters;
using Sim.Framework.Domain.Shared.Dependency;

namespace Sim.Framework.Core.Extensions;

/// <summary>
/// 模块配置扩展方法 — 从 ABP 依赖链自动发现程序集，无需在 appsettings.json 中配置 Modules。
/// 同时提供应用服务、领域服务与仓储的约定式自动注册。
/// </summary>
public static class ModuleConfigurationExtensions
{
    /// <summary>
    /// 从入口程序集的引用程序集中自动发现 Sim.* 模块，注册应用服务、领域服务、仓储到 DI，
    /// 并通过自定义约定暴露应用服务为 API 控制器（替代 ABP ConventionalControllers）。
    /// 不再依赖 appsettings.json 中的 Modules:Modules 配置节点。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="logger">日志记录器（可选）</param>
    /// <exception cref="InvalidOperationException">未找到任何 Sim.* 程序集时抛出</exception>
    public static void ConfigureConventionalControllers(
        this IServiceCollection services,
        ILogger? logger = null)
    {
        logger?.LogInformation("开始自动发现 Sim.* 模块程序集...");

        // TestHost and Swagger CLI may host the application from a non-Sim entry assembly.
        // Prefer the Sim assembly that invokes this framework extension in that case.
        var callingAssembly = Assembly.GetCallingAssembly();
        var entryAssembly = callingAssembly.GetName().Name is { } callingName && IsFrameworkAssembly(callingName)
            ? callingAssembly
            : Assembly.GetEntryAssembly()
              ?? throw new InvalidOperationException("无法确定入口程序集。");

        // 从入口程序集及其直接引用的程序集中，递归收集所有以 Sim. 开头的程序集
        var frameworkAssemblies = CollectFrameworkAssemblies(entryAssembly, logger);

        if (frameworkAssemblies.Count == 0)
        {
            logger?.LogError("未找到任何 Sim.* 程序集，请确认项目引用了至少一个 Sim.* 模块。");
            throw new InvalidOperationException("未找到任何 Sim.* 模块程序集。");
        }

        logger?.LogInformation("发现 {Count} 个 Sim.* 程序集", frameworkAssemblies.Count);

        var controllerOptions = new ConventionalControllerOptions();

        // 为每个 Application 层程序集登记路由根路径
        // 约定：Sim.{Name}.Application → rootPath = {name}
        foreach (var assembly in frameworkAssemblies)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyName == null)
            {
                continue;
            }

            var rootPath = DeriveRootPath(assemblyName);
            if (rootPath == null)
            {
                continue; // 非 Application 层，跳过路由注册
            }

            controllerOptions.AddModule(assembly, rootPath);
            logger?.LogDebug("模块路由已登记: {AssemblyName} → rootPath={RootPath}", assemblyName, rootPath);
        }

        RegisterAppServices(services, frameworkAssemblies, logger);
        RegisterAutoDependencies(services, frameworkAssemblies, logger);
        RegisterSqlSugarRepositories(services, frameworkAssemblies, logger);
        ConfigureMvc(services, frameworkAssemblies, controllerOptions);

        logger?.LogInformation("约定式注册完成：{AssemblyCount} 个程序集", frameworkAssemblies.Count);
    }

    /// <summary>
    /// 递归收集入口程序集及其直接引用的所有以 Sim. 开头的程序集。
    /// </summary>
    private static List<Assembly> CollectFrameworkAssemblies(Assembly entryAssembly, ILogger? logger)
    {
        var result = new List<Assembly>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Assembly>();

        queue.Enqueue(entryAssembly);
        visited.Add(entryAssembly.GetName().Name ?? string.Empty);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentName = current.GetName().Name;

            if (currentName is not null && IsFrameworkAssembly(currentName) && !result.Contains(current))
            {
                result.Add(current);
                logger?.LogDebug("收集 Sim 程序集: {AssemblyName}", currentName);
            }

            // 只遍历以 Sim. 开头的程序集的引用链（避免遍历整个 BCL）
            foreach (var referencedAssemblyName in current.GetReferencedAssemblies())
            {
                var refName = referencedAssemblyName.Name;
                if (refName is null || visited.Contains(refName))
                {
                    continue;
                }

                visited.Add(refName);

                if (IsFrameworkAssembly(refName))
                {
                    try
                    {
                        var refAssembly = Assembly.Load(referencedAssemblyName);
                        queue.Enqueue(refAssembly);
                    }
                    catch
                    {
                        logger?.LogDebug("无法加载引用程序集: {AssemblyName}", refName);
                    }
                }
            }
        }

        return result
            .OrderBy(a => a.GetName().Name)
            .ToList();
    }

    /// <summary>
    /// 判断程序集名称是否属于 Sim 框架（以 Sim. 开头）。
    /// </summary>
    private static bool IsFrameworkAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return false;
        }

        // Sim. 开头（如 Sim.Framework.Core）
        if (assemblyName.StartsWith("Sim.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 根据 Application 程序集名称推导路由根路径。
    /// 约定：Sim.{Name}.Application → rootPath = {name}
    /// 例如：Sim.System.Application → "system"，Sim.Approval.Application → "approval"
    /// </summary>
    private static string? DeriveRootPath(string assemblyName)
    {
        // 只处理 Application 层程序集
        if (!assemblyName.EndsWith(".Application", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // 去掉 Sim. 前缀
        const string prefix = "Sim.";
        if (!assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var withoutPrefix = assemblyName[prefix.Length..]; // e.g. "System.Application" or "Framework.Application"

        // 去掉 .Application 后缀
        const string suffix = ".Application";
        var name = withoutPrefix[..^suffix.Length]; // e.g. "System" or "Framework"

        return name.ToLowerInvariant();
    }

    /// <summary>
    /// 将模块程序集中的 IApplicationService 实现注册到 DI（Scoped），
    /// 同时注册其实现的应用服务接口。
    /// </summary>
    private static void RegisterAppServices(
        IServiceCollection services,
        IReadOnlyList<Assembly> assemblies,
        ILogger? logger)
    {
        foreach (var serviceType in assemblies.SelectMany(GetLoadableTypes).Where(AppServiceTypeHelper.IsAppService))
        {
            services.AddScoped(serviceType, sp => AppServiceFactory.Create(sp, serviceType));

            foreach (var interfaceType in GetAppServiceInterfaces(serviceType))
            {
                services.AddScoped(interfaceType, sp => sp.GetRequiredService(serviceType));
            }

            logger?.LogDebug("应用服务已注册: {ServiceType}", serviceType.FullName);
        }
    }

    /// <summary>
    /// 自动注册所有实现 IAutoDependency 的类及其生命周期标记接口。
    /// 注册策略：
    /// - 类自身按标记接口注册对应生命周期
    /// - 类实现的非标记接口（如 ISecurityManager）作为 forward 一并注册
    /// </summary>
    private static void RegisterAutoDependencies(
        IServiceCollection services,
        IReadOnlyList<Assembly> assemblies,
        ILogger? logger)
    {
        var candidateTypes = assemblies
            .SelectMany(GetLoadableTypes)
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic && !t.IsGenericTypeDefinition)
            .Where(t => typeof(IAutoDependency).IsAssignableFrom(t))
            .ToList();

        foreach (var implementationType in candidateTypes)
        {
            var lifetime = GetServiceLifetime(implementationType);

            // 注册实现类自身
            RegisterWithLifetime(services, implementationType, implementationType, lifetime);

            // 扫描实现类暴露的接口，自动 forward（跳过标记接口和 IAutoDependency 本身）
            foreach (var interfaceType in implementationType.GetInterfaces()
                         .Where(i => i != typeof(IAutoDependency)
                                  && i != typeof(ITransientDependency)
                                  && i != typeof(IScopedDependency)
                                  && i != typeof(ISingletonDependency)
                                  && i.IsPublic
                                  && !i.Name.StartsWith("IAuto")
                                  && i.Assembly.GetName().Name is not null
                                  && IsFrameworkAssembly(i.Assembly.GetName().Name)))
            {
                RegisterForwardWithLifetime(services, interfaceType, implementationType, lifetime);
            }

            logger?.LogDebug("自动注册: {Type} ({Lifetime})", implementationType.FullName, lifetime);
        }
    }

    /// <summary>
    /// 自动注册 ISqlSugarRepository&lt;T&gt; 的子接口及其实现类（Scoped）。
    /// 约定：I{Name}Repository → {Name}Repository（去掉 I 前缀）。
    /// 使用运行时类型名匹配避免 Sim.Framework.Core 对 Sim.SqlSugar.Core 的编译期依赖。
    /// </summary>
    private static void RegisterSqlSugarRepositories(
        IServiceCollection services,
        IReadOnlyList<Assembly> assemblies,
        ILogger? logger)
    {
        // 先收集所有程序集的类型，以支持跨程序集接口→实现匹配
        // （仓储接口在 Domain 层，实现在 SqlSugar 层）
        var allTypes = assemblies.SelectMany(GetLoadableTypes).ToList();

        // 找出所有 ISqlSugarRepository<> 的子接口（非开口泛型自身）
        // 通过运行时名称匹配避免编译期引用 Sim.SqlSugar.Core
        var repoInterfaces = allTypes
            .Where(t => t.IsInterface && t.IsPublic && !t.IsGenericTypeDefinition)
            .Where(t => t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition().Name == "ISqlSugarRepository`1"))
            .ToList();

        foreach (var repoInterface in repoInterfaces)
        {
            // 按命名约定跨程序集匹配实现类：I{Name}Repository → {Name}Repository
            var implName = repoInterface.Name.StartsWith('I')
                ? repoInterface.Name[1..]
                : repoInterface.Name;

            var implementationType = allTypes
                .FirstOrDefault(t => t.IsClass && !t.IsAbstract && t.IsPublic
                                    && t.Name.Equals(implName, StringComparison.Ordinal)
                                    && repoInterface.IsAssignableFrom(t));

            if (implementationType == null)
            {
                logger?.LogWarning("未找到仓储实现: {Interface} → {ImplName}", repoInterface.FullName, implName);
                continue;
            }

            services.AddScoped(implementationType);
            services.AddScoped(repoInterface, sp => sp.GetRequiredService(implementationType));

            logger?.LogDebug("仓储已注册: {Interface} → {Impl}", repoInterface.FullName, implementationType.FullName);
        }
    }

    private static ServiceLifetime GetServiceLifetime(Type type)
    {
        if (typeof(ISingletonDependency).IsAssignableFrom(type))
        {
            return ServiceLifetime.Singleton;
        }

        if (typeof(IScopedDependency).IsAssignableFrom(type))
        {
            return ServiceLifetime.Scoped;
        }

        // ITransientDependency 或仅 IAutoDependency → 默认 Transient
        return ServiceLifetime.Transient;
    }

    private static void RegisterWithLifetime(
        IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(serviceType, implementationType);
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped(serviceType, implementationType);
                break;
            default:
                services.AddTransient(serviceType, implementationType);
                break;
        }
    }

    private static void RegisterForwardWithLifetime(
        IServiceCollection services, Type interfaceType, Type implementationType, ServiceLifetime lifetime)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(interfaceType, sp => sp.GetRequiredService(implementationType));
                break;
            case ServiceLifetime.Scoped:
                services.AddScoped(interfaceType, sp => sp.GetRequiredService(implementationType));
                break;
            default:
                services.AddTransient(interfaceType, sp => sp.GetRequiredService(implementationType));
                break;
        }
    }

    private static IEnumerable<Type> GetAppServiceInterfaces(Type serviceType)
    {
        return serviceType.GetInterfaces().Where(i =>
            i != typeof(IApplicationService)
            && typeof(IApplicationService).IsAssignableFrom(i));
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    /// <summary>
    /// 配置 MVC：登记应用服务控制器发现、路由约定与激活器。
    /// </summary>
    private static void ConfigureMvc(
        IServiceCollection services,
        IReadOnlyList<Assembly> assemblies,
        ConventionalControllerOptions controllerOptions)
    {
        services.AddSingleton(controllerOptions);

        var mvcBuilder = services.AddControllers();

        mvcBuilder.ConfigureApplicationPartManager(manager =>
        {
            manager.FeatureProviders.Add(new AppServiceControllerFeatureProvider());

            foreach (var assembly in assemblies)
            {
                if (!manager.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == assembly))
                {
                    manager.ApplicationParts.Add(new AssemblyPart(assembly));
                }
            }
        });

        mvcBuilder.AddMvcOptions(options =>
        {
            options.Conventions.Add(new AppServiceConvention(controllerOptions));
            // 常规控制器没有 [ApiController]，需显式执行 ModelState 校验
            options.Filters.Add<ModelStateValidationFilter>();
            // 将 BusinessException/AuthorizationException 映射为 400/401 响应
            options.Filters.Add<ExceptionFilter>();
        });

        // [ValidateAntiForgeryToken] 通过工厂解析 MVC 内部的
        // ValidateAntiforgeryTokenAuthorizationFilter，AddControllers 不会注册它，
        // 这里按类型名注册（效果等同 AddControllersWithViews 的注册）
        var antiforgeryFilterType = typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute).Assembly
            .GetType("Microsoft.AspNetCore.Mvc.ViewFeatures.Filters.ValidateAntiforgeryTokenAuthorizationFilter");
        if (antiforgeryFilterType != null)
        {
            services.TryAddSingleton(antiforgeryFilterType);
        }

        services.Replace(ServiceDescriptor.Singleton<IControllerActivator, AppServiceControllerActivator>());
    }
}
