using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Approval.Domain.Runtime;

/// <summary>
/// 审批状态机守卫。集中实例与任务的可操作性判定，保证应用层无法绕过状态约束。
/// </summary>
public static class ApprovalStateMachine
{
    /// <summary>实例必须处于进行中（审批、退回、转交、加签等操作前调用）。</summary>
    public static void EnsureRunning(ApprovalInstance instance)
    {
        if (instance.Status != ApprovalInstanceStatus.Running)
        {
            throw new BusinessException(
                ApprovalErrorCodes.InstanceNotRunning,
                $"实例当前状态为 {instance.Status}，不可执行该操作。");
        }
    }

    /// <summary>实例必须处于被退回状态（重新提交前调用）。</summary>
    public static void EnsureReturned(ApprovalInstance instance)
    {
        if (instance.Status != ApprovalInstanceStatus.Returned)
        {
            throw new BusinessException(
                ApprovalErrorCodes.InstanceNotReturned,
                $"实例当前状态为 {instance.Status}，不可重新提交。");
        }
    }

    /// <summary>校验操作人是否为发起人。</summary>
    public static void EnsureInitiator(ApprovalInstance instance, Guid userId)
    {
        if (instance.InitiatorUserId != userId)
        {
            throw new BusinessException(ApprovalErrorCodes.NotInitiator, "只有发起人可以执行该操作。");
        }
    }

    /// <summary>撤回校验：仅发起人、进行中或被退回、流程允许撤回且未超出撤回时间窗。</summary>
    public static void EnsureCanWithdraw(ApprovalInstance instance, ApprovalFlowDefinition definition, Guid userId)
    {
        EnsureInitiator(instance, userId);
        if (instance.Status is not (ApprovalInstanceStatus.Running or ApprovalInstanceStatus.Returned))
        {
            throw new BusinessException(
                ApprovalErrorCodes.InstanceNotRunning,
                $"实例当前状态为 {instance.Status}，不可执行该操作。");
        }

        if (!definition.AllowWithdraw)
        {
            throw new BusinessException(
                ApprovalErrorCodes.WithdrawNotAllowed,
                $"流程 [{definition.Name}] 不允许撤回。");
        }

        if (IsWithdrawWindowExpired(instance, definition))
        {
            throw new BusinessException(
                ApprovalErrorCodes.WithdrawWindowExpired,
                $"流程 [{definition.Name}] 仅允许在发起后 {definition.WithdrawWindowHours} 小时内撤回。");
        }
    }

    /// <summary>是否可撤回。查询侧与 <see cref="EnsureCanWithdraw"/> 共用同一判定。</summary>
    public static bool CanWithdraw(ApprovalInstance instance, ApprovalFlowDefinition definition, Guid userId) =>
        instance.InitiatorUserId == userId
        && instance.Status is ApprovalInstanceStatus.Running or ApprovalInstanceStatus.Returned
        && definition.AllowWithdraw
        && !IsWithdrawWindowExpired(instance, definition);

    /// <summary>撤回时间窗是否已过（未配置时间窗表示不限）。</summary>
    private static bool IsWithdrawWindowExpired(ApprovalInstance instance, ApprovalFlowDefinition definition) =>
        definition.WithdrawWindowHours is > 0
        && DateTime.Now > instance.CreationTime.AddHours(definition.WithdrawWindowHours.Value);

    /// <summary>任务可处理校验：任务处于待处理且操作人是当前处理人（或指定代理人）。</summary>
    public static void EnsureTaskActionable(ApprovalTask task, Guid userId, bool allowDelegate = false)
    {
        if (task.Status != ApprovalTaskStatus.Pending)
        {
            throw new BusinessException(
                ApprovalErrorCodes.TaskAlreadyHandled,
                $"任务已处理，当前状态为 {task.Status}。");
        }

        if (task.AssigneeUserId != userId && !allowDelegate)
        {
            throw new BusinessException(ApprovalErrorCodes.NotTaskAssignee, "当前用户不是该任务的处理人。");
        }
    }

    /// <summary>节点配置开关校验（转交、加签、退回发起人）。</summary>
    public static void EnsureNodeAllows(ApprovalFlowNode node, string feature, bool allowed)
    {
        if (!allowed)
        {
            throw new BusinessException(
                feature switch
                {
                    "transfer" => ApprovalErrorCodes.TransferNotAllowed,
                    "add-sign" => ApprovalErrorCodes.AddSignNotAllowed,
                    _ => ApprovalErrorCodes.ReturnNotAllowed
                },
                $"节点 [{node.Name}] 不允许{feature}。");
        }
    }

    /// <summary>加签或转交目标校验。</summary>
    public static void EnsureValidTarget(Guid operatorUserId, Guid targetUserId)
    {
        if (targetUserId == Guid.Empty || targetUserId == operatorUserId)
        {
            throw new BusinessException(ApprovalErrorCodes.InvalidTargetUser, "目标用户不能为空或为当前处理人。");
        }
    }
}
