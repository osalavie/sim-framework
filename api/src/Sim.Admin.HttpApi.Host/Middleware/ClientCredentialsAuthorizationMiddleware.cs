using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Sim.Framework.Application.Contracts.Authorization;

namespace Sim.Admin.HttpApi.Host.Middleware;

/// <summary>
/// Client-credentials tokens have no user session. Restrict them to endpoints that require
/// an explicit permission, so they cannot reach authenticated-only user endpoints that rely
/// on a current user id.
/// </summary>
public sealed class ClientCredentialsAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            string.Equals(
                context.User.FindFirst("grant_type")?.Value,
                "client_credentials",
                StringComparison.Ordinal))
        {
            var endpoint = context.GetEndpoint();
            var hasPermission = endpoint?.Metadata.GetMetadata<PermissionAttribute>() is not null;

            if (!hasPermission &&
                endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>() is { } descriptor)
            {
                hasPermission =
                    descriptor.ControllerTypeInfo
                        .GetCustomAttributes(typeof(PermissionAttribute), true)
                        .Any()
                    || descriptor.MethodInfo
                        .GetCustomAttributes(typeof(PermissionAttribute), true)
                        .Any();
            }

            if (!hasPermission)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await next(context);
    }
}
