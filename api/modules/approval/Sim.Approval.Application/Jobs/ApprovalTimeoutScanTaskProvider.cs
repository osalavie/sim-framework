using Sim.Approval.Domain.Shared;
using Sim.System.Application.Contracts.ScheduleTask;

namespace Sim.Approval.Application.Jobs;

/// <summary>审批超时扫描内置定时任务定义。</summary>
public sealed class ApprovalTimeoutScanTaskProvider : IScheduleTaskDefinitionProvider
{
    public IReadOnlyCollection<ScheduleTaskDefinition> GetDefinitions() =>
    [
        new(
            ApprovalConsts.TimeoutScanTaskKey,
            ApprovalConsts.TimeoutScanTaskGroup,
            "审批超时扫描",
            "扫描已超时的审批待办并发出提醒通知",
            typeof(ApprovalTimeoutScanJob),
            ApprovalConsts.TimeoutScanTaskCron)
    ];
}
