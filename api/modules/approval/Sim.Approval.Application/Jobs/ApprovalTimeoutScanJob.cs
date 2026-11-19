using Microsoft.Extensions.Logging;
using Quartz;
using Sim.Approval.Application.Services;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.IRepositories;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Core.UnitOfWork;

namespace Sim.Approval.Application.Jobs;

/// <summary>
/// 审批超时扫描定时任务 —— 对待办中且已超过节点的超时时间的任务发出一次超时提醒。
/// 提醒仅在 Outbox 记录通知事件，不改变任务状态；同一任务只提醒一次。
/// </summary>
[DisallowConcurrentExecution]
public class ApprovalTimeoutScanJob(
    IApprovalTaskRepository taskRepository,
    IApprovalInstanceRepository instanceRepository,
    IApprovalRecordRepository recordRepository,
    ApprovalNotificationDispatcher notificationDispatcher,
    ISqlSugarUnitOfWork unitOfWork,
    ILogger<ApprovalTimeoutScanJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var now = DateTime.Now;

        // 后台任务无租户上下文，跨租户扫描
        var overdueTasks = await taskRepository.GetListAsync(
            item => item.Status == ApprovalTaskStatus.Pending
                    && item.DueAt != null
                    && item.DueAt <= now
                    && item.TimeoutNotifiedAt == null);
        if (overdueTasks.Count == 0)
        {
            return;
        }

        var instanceIds = overdueTasks.Select(item => item.InstanceId).Distinct().ToList();
        var instances = await instanceRepository.GetListAsync(item => instanceIds.Contains(item.Id));
        var instanceMap = instances.ToDictionary(item => item.Id);

        var notifiedCount = 0;
        foreach (var task in overdueTasks)
        {
            if (!instanceMap.TryGetValue(task.InstanceId, out var instance))
            {
                continue;
            }

            await unitOfWork.BeginAsync(cancellationToken);
            try
            {
                if (await taskRepository.TryMarkTimeoutNotifiedAsync(task.Id, now) == 0)
                {
                    await unitOfWork.RollbackAsync(CancellationToken.None);
                    continue;
                }

                await notificationDispatcher.PublishTaskTimeoutAsync(instance, task, cancellationToken);
                await recordRepository.InsertAsync(new ApprovalRecord
                {
                    Id = Guid.NewGuid(),
                    TenantId = instance.TenantId,
                    InstanceId = instance.Id,
                    TaskId = task.Id,
                    NodeKey = task.NodeKey,
                    NodeName = task.NodeName,
                    Action = ApprovalAction.TimeoutNotify,
                    OperatorUserId = null,
                    Comment = $"审批任务已超时（应处理时间 {task.DueAt:yyyy-MM-dd HH:mm:ss}）。"
                });
                await unitOfWork.CommitAsync(cancellationToken);
            }
            catch
            {
                await unitOfWork.RollbackAsync(CancellationToken.None);
                throw;
            }

            notifiedCount++;
        }

        logger.LogInformation("审批超时扫描完成：发现 {Overdue} 条超时待办，发出 {Notified} 条提醒", overdueTasks.Count, notifiedCount);
    }
}
