using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Domain.Shared;

namespace Sim.Approval.Application.Contracts.Ports;

/// <summary>
/// 业务模块的审批提交端口 — 业务模块在自身应用服务中调用，代表当前用户发起、重新提交或撤回审批。
/// 审批模块不引用任何业务模块实现，双方仅通过本契约协作。
/// </summary>
public interface IApprovalSubmitService
{
    /// <summary>
    /// 发起审批实例。发起人为当前登录用户。
    /// </summary>
    /// <param name="input">发起数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>发起结果。</returns>
    Task<ApprovalSubmitResult> SubmitAsync(ApprovalSubmitInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// 重新提交被退回的实例（仅发起人）。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <param name="input">重新提交数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>发起结果。</returns>
    Task<ApprovalSubmitResult> ResubmitAsync(
        Guid instanceId,
        ApprovalSubmitInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤回进行中的实例（仅发起人，且流程允许撤回）。
    /// </summary>
    /// <param name="instanceId">实例 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task WithdrawAsync(Guid instanceId, CancellationToken cancellationToken = default);
}

/// <summary>服务端发起审批的入参。</summary>
public sealed class ApprovalSubmitInput
{
    /// <summary>流程编码；为空时按业务类型解析默认流程。</summary>
    public string? DefinitionCode { get; set; }

    /// <summary>业务类型。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务对象标识。</summary>
    public string BusinessId { get; set; } = string.Empty;

    /// <summary>实例标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>业务上下文（条件求值与详情展示使用）。</summary>
    public Dictionary<string, string> Context { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 字段值列表（与 <see cref="Context"/> 二选一或并用，同键以本属性为准）。
    /// 展示元数据以业务类型声明的 <see cref="IApprovalBusinessDescriptor.Fields"/> 为准。
    /// </summary>
    public IReadOnlyList<ApprovalFieldValueDto> Fields { get; set; } = [];
}

/// <summary>发起审批的结果。</summary>
public sealed class ApprovalSubmitResult
{
    /// <summary>实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>实例编号。</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>实例状态。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>首个节点解析出的审批人。</summary>
    public List<Guid> CurrentAssigneeUserIds { get; set; } = [];
}

/// <summary>
/// 审批结果回调 — 业务模块实现该接口以在实例进入终态时更新自身业务状态。
/// 同一业务类型只能存在一个回调实现，审批引擎按 <see cref="BusinessType"/> 分发。
/// 回调在审批工作单元内同步执行，只允许同库、同工作单元内可回滚的数据变更；
/// 远程调用和其他不可回滚副作用必须通过 Outbox 集成事件异步执行。
/// </summary>
public interface IApprovalResultCallback
{
    /// <summary>业务类型（与发起时一致）。</summary>
    string BusinessType { get; }

    /// <summary>实例审批通过。</summary>
    Task OnApprovedAsync(ApprovalResultContext context, CancellationToken cancellationToken = default);

    /// <summary>实例被拒绝。</summary>
    Task OnRejectedAsync(ApprovalResultContext context, CancellationToken cancellationToken = default);

    /// <summary>实例被发起人撤回。</summary>
    Task OnWithdrawnAsync(ApprovalResultContext context, CancellationToken cancellationToken = default);

    /// <summary>实例被管理端终止。</summary>
    Task OnTerminatedAsync(ApprovalResultContext context, CancellationToken cancellationToken = default);
}

/// <summary>审批结果回调上下文。</summary>
public sealed class ApprovalResultContext
{
    /// <summary>实例 ID。</summary>
    public Guid InstanceId { get; init; }

    /// <summary>实例编号。</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>业务类型。</summary>
    public string BusinessType { get; init; } = string.Empty;

    /// <summary>业务对象标识。</summary>
    public string BusinessId { get; init; } = string.Empty;

    /// <summary>实例标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>发起人。</summary>
    public Guid InitiatorUserId { get; init; }

    /// <summary>触发终态的处理意见（拒绝原因等）。</summary>
    public string? Comment { get; init; }

    /// <summary>业务上下文快照。</summary>
    public Dictionary<string, string> Context { get; init; } = new(StringComparer.Ordinal);
}

/// <summary>
/// 业务类型审批描述符 — 业务模块实现该接口以声明业务类型、默认流程编码与业务字段清单。
/// </summary>
public interface IApprovalBusinessDescriptor
{
    /// <summary>业务类型（全局唯一，建议以模块为前缀，如 `quality.change-request`）。</summary>
    string BusinessType { get; }

    /// <summary>业务类型显示名。</summary>
    string DisplayName { get; }

    /// <summary>默认流程编码（发起时未显式指定流程编码时使用）。</summary>
    string? DefinitionCode { get; }

    /// <summary>
    /// 业务字段清单：详情页按此只读渲染，分支条件只能引用其中可比较的字段
    /// （<see cref="ApprovalFieldType.Attachment"/> 除外）。为空表示不校验条件字段。
    /// </summary>
    IReadOnlyList<ApprovalFieldDto> Fields { get; }
}

/// <summary>
/// 审批状态查询端口 — 业务模块按业务对象查询当前审批状态，用于列表角标与详情展示。
/// </summary>
public interface IApprovalStatusQuery
{
    /// <summary>
    /// 查询业务对象的活动或最近一次审批状态。
    /// </summary>
    /// <param name="businessType">业务类型。</param>
    /// <param name="businessId">业务对象标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>审批状态快照；不存在时返回 null。</returns>
    Task<ApprovalStatusSnapshot?> FindAsync(
        string businessType,
        string businessId,
        CancellationToken cancellationToken = default);
}

/// <summary>业务对象的审批状态快照。</summary>
public sealed class ApprovalStatusSnapshot
{
    /// <summary>实例 ID。</summary>
    public Guid InstanceId { get; init; }

    /// <summary>实例编号。</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>实例状态。</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>当前节点名称（进行中时返回）。</summary>
    public string? CurrentNodeName { get; init; }

    /// <summary>发起人。</summary>
    public Guid InitiatorUserId { get; init; }

    /// <summary>发起时间。</summary>
    public DateTime CreationTime { get; init; }

    /// <summary>结束时间。</summary>
    public DateTime? CompletedTime { get; init; }

    /// <summary>是否进行中。</summary>
    public bool IsRunning { get; init; }
}
