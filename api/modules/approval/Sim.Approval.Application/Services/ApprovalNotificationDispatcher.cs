using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.System.Application.Contracts.Notifications;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 审批通知分发器 —— 把待办、结果、催办、抄送与超时事件写入通知 Outbox，
/// 由通知模块按管理员配置的规则与模板投递。
/// </summary>
public sealed class ApprovalNotificationDispatcher(INotificationEventPublisher publisher)
{
    public async Task PublishTasksCreatedAsync(
        ApprovalInstance instance,
        IReadOnlyList<ApprovalTask> tasks,
        CancellationToken cancellationToken = default)
    {
        foreach (var task in tasks)
        {
            await PublishAsync(
                new NotificationEvent
                {
                    EventName = ApprovalConsts.NotificationEventTaskCreated,
                    TenantId = instance.TenantId,
                    CorrelationId = instance.Id.ToString("D"),
                    Recipients = [new NotificationRecipient { UserId = task.AssigneeUserId }],
                    Data = BuildInstanceData(instance, new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["taskId"] = task.Id.ToString("D"),
                        ["nodeName"] = task.NodeName,
                        ["taskType"] = task.TaskType.ToString(),
                        ["dueAt"] = task.DueAt?.ToString("O") ?? string.Empty
                    }),
                    IdempotencyKey = $"approval-task-created-{task.Id:D}"
                },
                cancellationToken);
        }
    }

    public async Task PublishFinishedAsync(
        ApprovalInstance instance,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        await PublishAsync(
            new NotificationEvent
            {
                EventName = ApprovalConsts.NotificationEventInstanceFinished,
                TenantId = instance.TenantId,
                CorrelationId = instance.Id.ToString("D"),
                Recipients = [new NotificationRecipient { UserId = instance.InitiatorUserId }],
                Data = BuildInstanceData(instance, new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["status"] = instance.Status.ToString(),
                    ["comment"] = comment ?? string.Empty
                }),
                IdempotencyKey = $"approval-instance-finished-{instance.Id:D}-{instance.Status}"
            },
            cancellationToken);
    }

    public async Task PublishUrgedAsync(
        ApprovalInstance instance,
        IReadOnlyList<ApprovalTask> pendingTasks,
        CancellationToken cancellationToken = default)
    {
        foreach (var task in pendingTasks)
        {
            await PublishAsync(
                new NotificationEvent
                {
                    EventName = ApprovalConsts.NotificationEventInstanceUrged,
                    TenantId = instance.TenantId,
                    CorrelationId = instance.Id.ToString("D"),
                    Recipients = [new NotificationRecipient { UserId = task.AssigneeUserId }],
                    Data = BuildInstanceData(instance, new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["taskId"] = task.Id.ToString("D"),
                        ["nodeName"] = task.NodeName
                    }),
                    IdempotencyKey = $"approval-instance-urged-{task.Id:D}-{DateTime.UtcNow:yyyyMMddHHmm}"
                },
                cancellationToken);
        }
    }

    public async Task PublishCcCreatedAsync(
        ApprovalInstance instance,
        IReadOnlyList<Guid> userIds,
        string nodeName,
        CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds)
        {
            await PublishAsync(
                new NotificationEvent
                {
                    EventName = ApprovalConsts.NotificationEventCcCreated,
                    TenantId = instance.TenantId,
                    CorrelationId = instance.Id.ToString("D"),
                    Recipients = [new NotificationRecipient { UserId = userId }],
                    Data = BuildInstanceData(instance, new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["nodeName"] = nodeName
                    }),
                    IdempotencyKey = $"approval-cc-created-{instance.Id:D}-{userId:D}"
                },
                cancellationToken);
        }
    }

    public async Task PublishTaskTimeoutAsync(
        ApprovalInstance instance,
        ApprovalTask task,
        CancellationToken cancellationToken = default)
    {
        await PublishAsync(
            new NotificationEvent
            {
                EventName = ApprovalConsts.NotificationEventTaskTimeout,
                TenantId = instance.TenantId,
                CorrelationId = instance.Id.ToString("D"),
                Recipients = [new NotificationRecipient { UserId = task.AssigneeUserId }],
                Data = BuildInstanceData(instance, new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["taskId"] = task.Id.ToString("D"),
                    ["nodeName"] = task.NodeName,
                    ["dueAt"] = task.DueAt?.ToString("O") ?? string.Empty
                }),
                IdempotencyKey = $"approval-task-timeout-{task.Id:D}"
            },
            cancellationToken);
    }

    private static Dictionary<string, string> BuildInstanceData(
        ApprovalInstance instance,
        Dictionary<string, string> extra)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["instanceId"] = instance.Id.ToString("D"),
            ["instanceNumber"] = instance.Number,
            ["title"] = instance.Title,
            ["definitionName"] = instance.DefinitionName,
            ["businessType"] = instance.BusinessType,
            ["businessId"] = instance.BusinessId
        };

        foreach (var pair in extra)
        {
            data[pair.Key] = pair.Value;
        }

        return data;
    }

    private async Task PublishAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken)
    {
        // Outbox 写入属于审批事务的一部分；失败必须向上抛出，使状态推进回滚并允许调用方重试。
        await publisher.PublishAsync(notificationEvent, cancellationToken);
    }
}
