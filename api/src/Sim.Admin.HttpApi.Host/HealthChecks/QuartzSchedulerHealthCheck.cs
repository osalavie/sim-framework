using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Quartz;

namespace Sim.Admin.HttpApi.Host.HealthChecks;

/// <summary>
/// Health check that verifies the Quartz scheduler is started and operational.
/// </summary>
internal sealed class QuartzSchedulerHealthCheck : IHealthCheck
{
    private readonly ISchedulerFactory _schedulerFactory;

    public QuartzSchedulerHealthCheck(ISchedulerFactory schedulerFactory)
    {
        _schedulerFactory = schedulerFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            if (scheduler == null)
            {
                return HealthCheckResult.Unhealthy("Quartz scheduler is null.");
            }

            if (scheduler.IsShutdown)
            {
                return HealthCheckResult.Unhealthy("Quartz scheduler is shut down.");
            }

            if (!scheduler.IsStarted)
            {
                return HealthCheckResult.Unhealthy("Quartz scheduler is not started.");
            }

            if (scheduler.InStandbyMode)
            {
                return HealthCheckResult.Degraded("Quartz scheduler is in standby mode.");
            }

            return HealthCheckResult.Healthy(
                $"Scheduler '{scheduler.SchedulerName}' is running.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Failed to query Quartz scheduler status.", ex);
        }
    }
}
