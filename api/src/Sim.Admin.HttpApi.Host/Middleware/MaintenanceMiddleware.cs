using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Sim.Admin.HttpApi.Host.Middleware;

/// <summary>
/// Blocks business requests while the deployment-level maintenance switch is enabled.
/// The switch is read from IConfiguration for every request so JSON configuration reloads take effect without restarting the host.
/// </summary>
public sealed class MaintenanceMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private static readonly string[] AlwaysAvailablePaths =
    [
        "/api/admin/site-status",
        "/health/live",
        "/health/ready",
        "/health-status",
    ];

    // 维护状态推送通道及其协商/传输子路径必须保持可用，
    // 否则维护结束后客户端无法收到关闭通知。
    private static readonly PathString[] AlwaysAvailablePathPrefixes =
    [
        "/hubs/site-status",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var maintenanceMode = configuration.GetValue("Site:MaintenanceMode", false);
        if (!maintenanceMode || IsAlwaysAvailable(context.Request.Path))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.CacheControl = "no-store";

        await context.Response.WriteAsJsonAsync(new
        {
            maintenanceMode = true,
            maintenanceMessage = string.IsNullOrWhiteSpace(configuration["Site:MaintenanceMessage"])
                ? null
                : configuration["Site:MaintenanceMessage"],
        });
    }

    private static bool IsAlwaysAvailable(PathString path)
    {
        foreach (var availablePath in AlwaysAvailablePaths)
        {
            if (path.Equals(availablePath))
            {
                return true;
            }
        }

        foreach (var prefix in AlwaysAvailablePathPrefixes)
        {
            if (path.StartsWithSegments(prefix))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Extension method to register the middleware in the pipeline.
/// </summary>
public static class MaintenanceMiddlewareExtensions
{
    public static IApplicationBuilder UseMaintenance(this IApplicationBuilder app)
    {
        return app.UseMiddleware<MaintenanceMiddleware>();
    }
}
