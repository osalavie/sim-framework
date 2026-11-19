using Microsoft.Extensions.Logging;
using Quartz;

namespace Sim.System.Application.Jobs;

[DisallowConcurrentExecution]
public sealed class SystemHeartbeatJob(ILogger<SystemHeartbeatJob> logger) :
    IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("System worker heartbeat completed");
        return Task.CompletedTask;
    }
}
