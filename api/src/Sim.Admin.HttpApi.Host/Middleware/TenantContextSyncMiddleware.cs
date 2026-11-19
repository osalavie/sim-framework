using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Sim.Framework.Domain;
using Sim.Framework.Domain.Shared.Security;
using System.Threading.Tasks;

namespace Sim.Admin.HttpApi.Host.Middleware;

/// <summary>
/// Syncs ICurrentTenant to TenantContext so SqlSugar query filters can access it.
/// Must run after authentication, because the tenant id is read from the JWT principal.
/// </summary>
public sealed class TenantContextSyncMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextSyncMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, ICurrentTenant currentTenant)
    {
        TenantContext.CurrentTenantId = currentTenant.Id;
        await _next(httpContext);
    }
}

/// <summary>
/// Extension method to register the TenantContext sync middleware.
/// </summary>
public static class TenantContextSyncMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContextSync(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextSyncMiddleware>();
    }
}
