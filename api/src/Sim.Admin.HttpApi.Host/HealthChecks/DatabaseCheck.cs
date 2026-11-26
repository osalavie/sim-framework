using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SqlSugar;

namespace Sim.Admin.HttpApi.Host.HealthChecks;

public class DatabaseCheck(ISqlSugarClient database) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            database.Ado.CheckConnection();
            return Task.FromResult(HealthCheckResult.Healthy("Database connection is available."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Database connection is unavailable."));
        }
    }
}
