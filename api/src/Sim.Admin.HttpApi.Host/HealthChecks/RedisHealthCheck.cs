using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Sim.Admin.HttpApi.Host.HealthChecks;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? _redis;

    public RedisHealthCheck(IServiceProvider serviceProvider)
    {
        _redis = serviceProvider.GetService<IConnectionMultiplexer>();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_redis == null)
        {
            return HealthCheckResult.Healthy("Redis not configured (using local cache).");
        }

        try
        {
            if (!_redis.IsConnected)
            {
                return HealthCheckResult.Degraded("Redis connection is not established.");
            }

            var db = _redis.GetDatabase();
            await db.PingAsync();
            return HealthCheckResult.Healthy("Redis is responsive.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis ping failed.", ex);
        }
    }
}
