using System;
using System.Linq;
using Sim.Admin.Domain.Data;
using Sim.Approval.Application.Seeds;
using Sim.System.Domain.Shared.Permissions;
using Xunit;

namespace Sim.Security.Tests;

/// <summary>
/// 种子路由默认权限映射守卫：路由匹配到的权限码必须都已登记在权限清单，
/// 避免种子直接写入无效权限码导致页面仅超级权限可见。
/// </summary>
public class RoutePermissionCatalogTests
{
    [Fact]
    public void EveryMappedRoutePermissionExistsInCatalog()
    {
        var registered = PermissionCatalog.All
            .Select(definition => definition.Code)
            .Concat(ApprovalModuleSeedContributor.PermissionSeeds.Select(seed => seed.Code))
            .ToHashSet(StringComparer.Ordinal);

        var routePermissionCodes = RoutePermissionCatalog.EnumeratePermissionCodes()
            .Concat(ApprovalModuleSeedContributor.RouteSeeds
                .Where(seed => !string.IsNullOrWhiteSpace(seed.PermissionCode))
                .Select(seed => seed.PermissionCode!));

        var missing = routePermissionCodes
            .Where(code => !registered.Contains(code))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "以下路由默认权限码未登记在 PermissionCatalog：" + string.Join(", ", missing));
    }

    [Fact]
    public void KeySeededRoutesMapToTheirReadPermissions()
    {
        Assert.Equal("system:user:query", RoutePermissionCatalog.GetPermissionCode("UserManagement"));
        Assert.Equal(
            "system:file-storage:manage",
            RoutePermissionCatalog.GetPermissionCode("FileStorageManagement"));
        Assert.Null(RoutePermissionCatalog.GetPermissionCode("ApprovalTodo"));

        // 根路由按子页面权限并集放行；含个人页的根路由保持不限制
        Assert.Contains("system:file-storage:manage", RoutePermissionCatalog.GetPermissionCode("System"));
        Assert.Contains(TenantPermissions.Manage, RoutePermissionCatalog.GetPermissionCode("System"));
        Assert.Contains(OrganizationPermissions.Manage, RoutePermissionCatalog.GetPermissionCode("System"));
        Assert.Contains("system:user:query", RoutePermissionCatalog.GetPermissionCode("Authorization"));
        Assert.DoesNotContain(TenantPermissions.Manage, RoutePermissionCatalog.GetPermissionCode("Authorization"));
        Assert.DoesNotContain(OrganizationPermissions.Manage, RoutePermissionCatalog.GetPermissionCode("Authorization"));
        Assert.Null(RoutePermissionCatalog.GetPermissionCode("Approval"));
    }
}
