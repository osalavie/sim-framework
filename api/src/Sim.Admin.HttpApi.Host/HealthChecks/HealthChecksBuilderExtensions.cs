using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Sim.Admin.HttpApi.Host.HealthChecks;

/// <summary>
/// 健康检查端点注册表。由 <see cref="HealthChecksBuilderExtensions.AddHostHealthChecks"/>
/// 在 ConfigureServices 阶段填充，在 Host 模块的 UseEndpoints 中统一映射。
/// </summary>
public sealed class HealthCheckEndpointOptions
{
    public List<(string Path, Func<HealthCheckRegistration, bool> Predicate)> Endpoints { get; } = [];
}

public static class HealthChecksBuilderExtensions
{
    public static void AddHostHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
            .AddCheck<DatabaseCheck>("database", tags: ["ready", "database"])
            .AddCheck<QuartzSchedulerHealthCheck>("quartz", tags: ["ready"])
            .AddCheck<RedisHealthCheck>("redis", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

        var configuration = services.GetConfiguration();
        var healthCheckUrl = configuration["App:HealthCheckUrl"];

        if (string.IsNullOrEmpty(healthCheckUrl))
        {
            healthCheckUrl = "/health-status";
        }

        services.ConfigureHealthCheckEndpoint("/health/live", registration => registration.Tags.Contains("live"));
        services.ConfigureHealthCheckEndpoint("/health/ready", registration => registration.Tags.Contains("ready"));
        services.ConfigureHealthCheckEndpoint(healthCheckUrl, _ => true);
    }

    private static IServiceCollection ConfigureHealthCheckEndpoint(
        this IServiceCollection services,
        string path,
        Func<HealthCheckRegistration, bool> predicate)
    {
        services.Configure<HealthCheckEndpointOptions>(options =>
        {
            options.Endpoints.Add((path.StartsWith('/') ? path : $"/{path}", predicate));
        });

        return services;
    }

    /// <summary>
    /// 在 Host 模块的 UseEndpoints 中调用，映射已注册的健康检查端点。
    /// </summary>
    public static void MapHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<HealthCheckEndpointOptions>>().Value;

        foreach (var (path, predicate) in options.Endpoints)
        {
            endpoints.MapHealthChecks(
                new PathString(path),
                new HealthCheckOptions
                {
                    Predicate = predicate,
                    ResponseWriter = async (context, report) =>
                    {
                        context.Response.ContentType = "application/json";
                        var payload = new
                        {
                            status = report.Status.ToString(),
                            totalDuration = report.TotalDuration,
                            entries = report.Entries.Select(entry => new
                            {
                                name = entry.Key,
                                status = entry.Value.Status.ToString(),
                                duration = entry.Value.Duration,
                                description = entry.Value.Description,
                            }),
                        };

                        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
                    },
                    AllowCachingResponses = false,
                });
        }
    }
}
