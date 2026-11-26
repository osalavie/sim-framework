using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace Sim.Admin.HttpApi.Host.HealthChecks;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public RabbitMqHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var provider = _configuration["Framework:Messaging:Provider"];
        if (!string.Equals(provider, "RabbitMQ", StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Healthy("RabbitMQ not configured (using local messaging).");
        }

        var connectionString = _configuration["Framework:Messaging:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection string is empty.");
        }

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
            };
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ connection successful.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ connection failed.", ex);
        }
    }
}
