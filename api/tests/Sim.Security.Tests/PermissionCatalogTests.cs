using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Sim.Admin.Domain.Data;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.System.Domain.Shared;
using Xunit;
using Sim.System.Domain.Shared.Permissions;

namespace Sim.Security.Tests;

/// <summary>
/// 权限清单守卫：保证服务上 <c>[Permission]</c> 使用的权限码都已登记到 <see cref="PermissionCatalog"/>，
/// 同时保证清单本身格式统一、无重复，避免管理端出现“权限不全 / 权限码格式不一”。
/// </summary>
public class PermissionCatalogTests
{
    private static readonly Regex PermissionCodePattern = new(
        @"^(\*:\*:\*|[a-z][a-z0-9-]*(:[a-z0-9-]+)+)$",
        RegexOptions.Compiled);

    private static IEnumerable<PermissionCatalog.PermissionDefinition> AllDefinitions()
    {
        foreach (var definition in PermissionCatalog.All)
        {
            yield return definition;
        }

        foreach (var definition in DiscoverModulePermissions())
        {
            yield return definition;
        }
    }

    /// <summary>
    /// 业务模块通过 ModuleSeedContributor 自描述权限；测试只按约定发现，
    /// 不直接引用任何业务程序集。
    /// </summary>
    private static IEnumerable<PermissionCatalog.PermissionDefinition> DiscoverModulePermissions()
    {
        foreach (var assembly in LoadPermissionAssemblies())
        {
            foreach (var type in GetLoadableTypes(assembly)
                         .Where(type => type is { IsClass: true, IsAbstract: false } &&
                                        type.Name.EndsWith("ModuleSeedContributor", StringComparison.Ordinal)))
            {
                var property = type.GetProperty("PermissionSeeds", BindingFlags.Public | BindingFlags.Static);
                if (property?.GetValue(null) is not global::System.Collections.IEnumerable seeds) continue;

                foreach (var seed in seeds)
                {
                    var seedType = seed?.GetType();
                    if (seedType?.GetProperty("Code")?.GetValue(seed) is not string code ||
                        seedType.GetProperty("Name")?.GetValue(seed) is not string name ||
                        seedType.GetProperty("Description")?.GetValue(seed) is not string description)
                    {
                        continue;
                    }

                    yield return new PermissionCatalog.PermissionDefinition(code, name, description);
                }
            }
        }
    }

    [Fact]
    public void EveryPermissionCodeUsedByAttributesIsRegisteredInCatalog()
    {
        var registered = AllDefinitions().Select(definition => definition.Code)
            .ToHashSet(StringComparer.Ordinal);

        var missing = CollectAttributePermissionCodes()
            .Where(code => !registered.Contains(code))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "以下权限码被 [Permission] 使用但未登记在 PermissionCatalog：" + string.Join(", ", missing));
    }

    [Fact]
    public void AttributePermissionCodesAreDiscoverableAcrossModules()
    {
        var codes = CollectAttributePermissionCodes().ToList();

        // 防止反射扫描失效（例如程序集未复制到测试输出目录）导致上面的守卫测试空跑
        Assert.True(codes.Count >= 20, $"仅扫描到 {codes.Count} 个 [Permission] 权限码，请检查测试程序集加载。");
        Assert.Contains(SystemPermissions.User.Query, codes);
        Assert.Contains(ApprovalPermissions.MonitorInstance, codes);
        Assert.Contains("system:worker:query", codes);
    }

    [Fact]
    public void CatalogCodesAreUnique()
    {
        var duplicates = AllDefinitions()
            .GroupBy(definition => definition.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, "权限清单存在重复权限码：" + string.Join(", ", duplicates));
    }

    [Fact]
    public void CatalogCodesFollowModuleResourceActionConvention()
    {
        var invalid = AllDefinitions()
            .Where(definition => !PermissionCodePattern.IsMatch(definition.Code))
            .Select(definition => definition.Code)
            .ToList();

        Assert.True(invalid.Count == 0, "权限码不符合 module:resource:action 约定：" + string.Join(", ", invalid));
    }

    [Fact]
    public void CatalogDefinitionsCarryNameAndDescription()
    {
        var incomplete = AllDefinitions()
            .Where(definition => string.IsNullOrWhiteSpace(definition.Name)
                                 || string.IsNullOrWhiteSpace(definition.Description))
            .Select(definition => definition.Code)
            .ToList();

        Assert.True(incomplete.Count == 0, "权限清单缺少名称或描述：" + string.Join(", ", incomplete));
    }

    private static IEnumerable<string> CollectAttributePermissionCodes()
    {
        var codes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var assembly in LoadPermissionAssemblies())
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                foreach (var attribute in type.GetCustomAttributes<PermissionAttribute>(inherit: true))
                {
                    codes.Add(attribute.Code);
                }

                foreach (var method in type.GetMethods(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                             | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    foreach (var attribute in method.GetCustomAttributes<PermissionAttribute>(inherit: true))
                    {
                        codes.Add(attribute.Code);
                    }
                }
            }
        }

        return codes;
    }

    private static IEnumerable<Assembly> LoadPermissionAssemblies()
    {
        var directory = AppContext.BaseDirectory;

        return Directory.EnumerateFiles(directory, "Sim.*.dll")
            .Where(file => file.EndsWith(".Application.dll", StringComparison.OrdinalIgnoreCase)
                           || file.EndsWith("Sim.Admin.HttpApi.Host.dll", StringComparison.OrdinalIgnoreCase))
            .Select(Assembly.LoadFrom)
            .ToList();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null).Cast<Type>();
        }
    }
}
