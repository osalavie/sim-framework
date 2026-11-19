using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Sim.Admin.HttpApi.Host.Middleware;

/// <summary>
/// Adds production security response headers beyond ABP defaults.
/// ABP provides X-Content-Type-Options: nosniff and X-Frame-Options: SAMEORIGIN.
/// This middleware adds CSP, Referrer-Policy, and Permissions-Policy;
/// the CSP value is configured through Security:Headers:ContentSecurityPolicy.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly string? _contentSecurityPolicy =
        configuration["Security:Headers:ContentSecurityPolicy"];

    private static readonly StringValues Nosniff = "nosniff";
    private static readonly StringValues SameOrigin = "SAMEORIGIN";
    private static readonly StringValues StrictOriginWhenCrossOrigin = "strict-origin-when-cross-origin";

    public Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;

        // Content-Security-Policy: XSS and injection protection; empty config leaves the header to the edge proxy
        if (!string.IsNullOrWhiteSpace(_contentSecurityPolicy))
        {
            response.Headers["Content-Security-Policy"] = _contentSecurityPolicy;
        }

        // Referrer-Policy: limit cross-origin referrer leakage
        response.Headers["Referrer-Policy"] = StrictOriginWhenCrossOrigin;

        // Permissions-Policy: disable unnecessary browser features
        response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

        // HSTS header is set by reverse proxy (Nginx/IIS) or via app.UseHsts()
        // to ensure it covers all routes with the correct max-age

        return next(context);
    }
}

/// <summary>
/// Extension method to register the middleware in the pipeline.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
