using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Sim.OpenPlatform.Domain.Entities;
using Sim.SqlSugar.Core.Repositories;

namespace Sim.OpenPlatform.Application.Auditing;

/// <summary>
/// 开放平台逐请求调用审计 — 对携带 client_credentials Token（含 client_id Claim）的请求，
/// 记录方法、路径、状态码、客户端 IP 与耗时到 openplatform_api_call_log。
/// 仅在认证中间件之后运行，确保 User Claims 可用。
/// </summary>
public sealed class OpenPlatformCallAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISqlSugarRepository<OpenPlatformApiCallLog> repository)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            var clientId = context.User.FindFirst("client_id")?.Value;
            if (!string.IsNullOrWhiteSpace(clientId))
            {
                var applicationIdText = context.User.FindFirst("application_id")?.Value;
                Guid.TryParse(applicationIdText, out var applicationId);

                await repository.InsertAsync(new OpenPlatformApiCallLog
                {
                    ApplicationId = applicationId,
                    AppId = clientId,
                    Method = context.Request.Method,
                    Path = context.Request.Path.Value is { } path
                        ? path[..Math.Min(path.Length, 2048)]
                        : string.Empty,
                    StatusCode = context.Response.StatusCode,
                    ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                    DurationMs = stopwatch.ElapsedMilliseconds
                });
            }
        }
    }
}
