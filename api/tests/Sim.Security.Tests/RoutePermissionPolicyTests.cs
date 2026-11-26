using System;
using System.Collections.Generic;
using Sim.System.Domain.Shared.Permissions;
using Xunit;

namespace Sim.Security.Tests;

/// <summary>
/// 路由权限码匹配守卫：空权限码不限制访问，多个权限码按“任一/全部满足”判定。
/// </summary>
public class RoutePermissionPolicyTests
{
    private static HashSet<string> Codes(params string[] codes) =>
        new(codes, StringComparer.Ordinal);

    [Fact]
    public void EmptyRequirementAllowsAccess()
    {
        Assert.True(RoutePermissionPolicy.CanAccess(null, Codes(), requireAll: false));
        Assert.True(RoutePermissionPolicy.CanAccess("  ", Codes(), requireAll: true));
    }

    [Fact]
    public void AnyModeRequiresOneMatchingCode()
    {
        Assert.True(RoutePermissionPolicy.CanAccess("system:route:create,system:route:update", Codes("system:route:update"), requireAll: false));
        Assert.False(RoutePermissionPolicy.CanAccess("system:route:create,system:route:update", Codes("system:route:delete"), requireAll: false));
    }

    [Fact]
    public void AllModeRequiresEveryCode()
    {
        Assert.False(RoutePermissionPolicy.CanAccess("system:route:create,system:route:update", Codes("system:route:create"), requireAll: true));
        Assert.True(RoutePermissionPolicy.CanAccess("system:route:create,system:route:update", Codes("system:route:create", "system:route:update"), requireAll: true));
    }

    [Fact]
    public void SuperPermissionMatchesAnyRequirement()
    {
        Assert.True(RoutePermissionPolicy.CanAccess("system:route:create,system:route:update", Codes("*:*:*"), requireAll: true));
    }

    [Fact]
    public void CodesAreTrimmedAndBlankEntriesIgnored()
    {
        Assert.True(RoutePermissionPolicy.CanAccess(" system:route:create , ,", Codes("system:route:create"), requireAll: true));
    }
}
