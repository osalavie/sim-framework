using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.System.Domain.Shared.Consts;

namespace Sim.System.Application.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionAttribute>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionAttribute requirement)
    {
        var permissions = context.User.FindAll(TokenTypeConst.Permission).Select(claim => claim.Value);
        var isHostBuiltIn = context.User.HasClaim(TokenTypeConst.IsBuiltInRole, bool.TrueString)
                            && !context.User.HasClaim(claim => claim.Type == TokenTypeConst.TenantId);

        if (isHostBuiltIn ||
            permissions.Contains("*:*:*") ||
            permissions.Contains(requirement.Code))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
