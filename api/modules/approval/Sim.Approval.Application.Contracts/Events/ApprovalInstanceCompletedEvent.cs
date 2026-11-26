namespace Sim.Approval.Application.Contracts.Events;

/// <summary>
/// 审批实例终态集成事件 —— 实例进入 Approved / Rejected / Withdrawn / Terminated 时写入消息 Outbox。
/// 业务模块注册 <c>IMessageHandler&lt;ApprovalInstanceCompletedEvent&gt;</c> 异步消费，
/// 不再通过审批事务内的同步回调更新业务数据。
/// </summary>
public sealed class ApprovalInstanceCompletedEvent
{
    /// <summary>审批实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>审批实例编号。</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>流程编码（发起时快照）。</summary>
    public string DefinitionCode { get; set; } = string.Empty;

    /// <summary>流程名称（发起时快照）。</summary>
    public string DefinitionName { get; set; } = string.Empty;

    /// <summary>流程版本（发起时快照）。</summary>
    public int DefinitionVersion { get; set; }

    /// <summary>业务类型。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务对象标识。</summary>
    public string BusinessId { get; set; } = string.Empty;

    /// <summary>实例标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>发起人用户 ID。</summary>
    public Guid InitiatorUserId { get; set; }

    /// <summary>终态状态：Approved / Rejected / Withdrawn / Terminated。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>触发终态的处理意见（拒绝原因、终止原因等）。</summary>
    public string? Comment { get; set; }

    /// <summary>结束时间。</summary>
    public DateTime CompletedTime { get; set; }

    /// <summary>业务字段快照（条件求值与业务展示使用）。</summary>
    public Dictionary<string, string> Context { get; set; } = new(StringComparer.Ordinal);
}
