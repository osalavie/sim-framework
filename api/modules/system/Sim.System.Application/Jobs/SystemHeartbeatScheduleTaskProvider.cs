using Sim.System.Application.Contracts.ScheduleTask;

namespace Sim.System.Application.Jobs;

public sealed class SystemHeartbeatScheduleTaskProvider :
    IScheduleTaskDefinitionProvider
{
    public IReadOnlyCollection<ScheduleTaskDefinition> GetDefinitions() =>
    [
        new(
            "SystemHeartbeatJob",
            "Worker",
            "系统 Worker 心跳",
            "由 System 模块定义的内置任务，用于验证调度器运行状态。",
            typeof(SystemHeartbeatJob),
            "0 0/5 * * * ?")
    ];
}
