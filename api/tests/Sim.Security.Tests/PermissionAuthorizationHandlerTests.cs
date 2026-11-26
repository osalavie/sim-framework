using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.System.Application.Authorization;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Domain.Shared.Permissions;

namespace Sim.Security.Tests;

public class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task ExactPermissionSucceeds()
    {
        var context = CreateContext(new Claim(TokenTypeConst.Permission, SystemPermissions.User.Query));

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task WildcardAndBuiltInRoleSucceed()
    {
        var wildcard = CreateContext(new Claim(TokenTypeConst.Permission, "*:*:*"));
        var admin = CreateContext(new Claim(TokenTypeConst.IsBuiltInRole, bool.TrueString));
        var handler = new PermissionAuthorizationHandler();

        await handler.HandleAsync(wildcard);
        await handler.HandleAsync(admin);

        Assert.True(wildcard.HasSucceeded);
        Assert.True(admin.HasSucceeded);
    }

    [Fact]
    public async Task BuiltInRoleCodeAloneDoesNotGrantAccess()
    {
        var context = CreateContext(new Claim(ClaimTypes.Role, RoleConst.Admin));

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task MissingPermissionIsDenied()
    {
        var context = CreateContext(new Claim(TokenTypeConst.Permission, SystemPermissions.User.Create));

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext CreateContext(params Claim[] claims)
    {
        var requirement = new PermissionAttribute(SystemPermissions.User.Query);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return new AuthorizationHandlerContext([requirement], principal, null);
    }
}
