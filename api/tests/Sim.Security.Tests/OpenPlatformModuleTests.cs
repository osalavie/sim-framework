using Sim.OpenPlatform.Application.Seeds;
using Sim.OpenPlatform.Domain.Shared;
using Xunit;

namespace Sim.Security.Tests;

public sealed class OpenPlatformModuleTests
{
    [Theory]
    [InlineData("*:*:*")]
    [InlineData("openplatform:manage")]
    [InlineData("openplatform:access-log:view")]
    [InlineData("OpenPlatform:Manage")]
    [InlineData("")]
    public void ReservedScopesCannotBeGranted(string code) =>
        Assert.False(OpenPlatformScopePolicy.IsGrantable(code));

    [Fact]
    public void ScopesMustExistInCatalogAndExcludeReservedPermissions()
    {
        IReadOnlySet<string> available = new HashSet<string>(StringComparer.Ordinal)
        {
            "blog:article:create",
            "*:*:*",
            "openplatform:manage"
        };

        Assert.True(OpenPlatformScopePolicy.AreGrantable(["blog:article:create"], available));
        Assert.False(OpenPlatformScopePolicy.AreGrantable(["blog:article:delete"], available));
        Assert.False(OpenPlatformScopePolicy.AreGrantable(["*:*:*"], available));
        Assert.False(OpenPlatformScopePolicy.AreGrantable(["openplatform:manage"], available));
    }

    [Fact]
    public void ModuleSeedsBindBothPagesToPermissions()
    {
        var module = Assert.Single(OpenPlatformModuleSeedContributor.ModuleSeeds);
        Assert.Equal("openplatform", module.Name);

        Assert.Contains(OpenPlatformModuleSeedContributor.PermissionSeeds,
            seed => seed.Code == OpenPlatformPermissions.Manage);
        Assert.Contains(OpenPlatformModuleSeedContributor.PermissionSeeds,
            seed => seed.Code == OpenPlatformPermissions.ViewAccessLogs);

        Assert.Contains(OpenPlatformModuleSeedContributor.RouteSeeds,
            route => route.Name == "OpenPlatformApplications" &&
                     route.PermissionCode == OpenPlatformPermissions.Manage);
        Assert.Contains(OpenPlatformModuleSeedContributor.RouteSeeds,
            route => route.Name == "OpenPlatformAccessLogs" &&
                     route.PermissionCode == OpenPlatformPermissions.ViewAccessLogs);
    }
}
