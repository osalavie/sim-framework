namespace Sim.Approval.Domain.Shared;

/// <summary>
/// 审批模块错误码。仅在本文件声明，业务代码必须引用常量而不是硬编码。
/// </summary>
public static class ApprovalErrorCodes
{
    /// <summary>流程定义不存在。</summary>
    public const string DefinitionNotFound = "Sim:Approval:DefinitionNotFound";

    /// <summary>同一流程编码下的版本号已存在。</summary>
    public const string DefinitionVersionDuplicated = "Sim:Approval:DefinitionVersionDuplicated";

    /// <summary>已发布的流程定义不可修改，需基于新版本调整。</summary>
    public const string DefinitionPublished = "Sim:Approval:DefinitionPublished";

    /// <summary>流程定义尚未发布，不能发起实例。</summary>
    public const string DefinitionNotPublished = "Sim:Approval:DefinitionNotPublished";

    /// <summary>流程定义缺少审批节点。</summary>
    public const string DefinitionNoApprovalNode = "Sim:Approval:DefinitionNoApprovalNode";

    /// <summary>流程定义的节点标识重复。</summary>
    public const string DefinitionNodeKeyDuplicated = "Sim:Approval:DefinitionNodeKeyDuplicated";

    /// <summary>流程定义存在进行中的实例，不能停用。</summary>
    public const string DefinitionInUse = "Sim:Approval:DefinitionInUse";

    /// <summary>流程定义节点不存在。</summary>
    public const string NodeNotFound = "Sim:Approval:NodeNotFound";

    /// <summary>流程定义图结构不合法（缺开始/结束节点、孤立节点、非法出边等）。</summary>
    public const string DefinitionInvalid = "Sim:Approval:DefinitionInvalid";

    /// <summary>节点条件表达式不合法。</summary>
    public const string ConditionInvalid = "Sim:Approval:ConditionInvalid";

    /// <summary>节点未解析出任何审批人。</summary>
    public const string ApproverUnresolved = "Sim:Approval:ApproverUnresolved";

    /// <summary>审批实例不存在。</summary>
    public const string InstanceNotFound = "Sim:Approval:InstanceNotFound";

    /// <summary>同一业务对象已存在进行中的审批实例。</summary>
    public const string InstanceAlreadyRunning = "Sim:Approval:InstanceAlreadyRunning";

    /// <summary>审批实例不在可操作状态。</summary>
    public const string InstanceNotRunning = "Sim:Approval:InstanceNotRunning";

    /// <summary>审批实例不在被退回状态，不能重新提交。</summary>
    public const string InstanceNotReturned = "Sim:Approval:InstanceNotReturned";

    /// <summary>当前用户不是该实例的发起人。</summary>
    public const string NotInitiator = "Sim:Approval:NotInitiator";

    /// <summary>当前方案不允许撤回。</summary>
    public const string WithdrawNotAllowed = "Sim:Approval:WithdrawNotAllowed";

    /// <summary>已超过流程定义的撤回时间窗。</summary>
    public const string WithdrawWindowExpired = "Sim:Approval:WithdrawWindowExpired";

    /// <summary>催办过于频繁（同一实例的催办间隔未到）。</summary>
    public const string UrgeTooFrequent = "Sim:Approval:UrgeTooFrequent";

    /// <summary>当前方案不允许退回发起人。</summary>
    public const string ReturnNotAllowed = "Sim:Approval:ReturnNotAllowed";

    /// <summary>尚无已通过的上一节点，不能退回上一步。</summary>
    public const string ReturnPreviousUnavailable = "Sim:Approval:ReturnPreviousUnavailable";

    /// <summary>当前方案不允许转交。</summary>
    public const string TransferNotAllowed = "Sim:Approval:TransferNotAllowed";

    /// <summary>当前方案不允许加签。</summary>
    public const string AddSignNotAllowed = "Sim:Approval:AddSignNotAllowed";

    /// <summary>转交或加签的目标用户不能是当前处理人。</summary>
    public const string InvalidTargetUser = "Sim:Approval:InvalidTargetUser";

    /// <summary>审批任务不存在。</summary>
    public const string TaskNotFound = "Sim:Approval:TaskNotFound";

    /// <summary>审批附件不存在，或当前用户无权访问该实例的附件。</summary>
    public const string AttachmentNotFound = "Sim:Approval:AttachmentNotFound";

    /// <summary>审批任务已被处理。</summary>
    public const string TaskAlreadyHandled = "Sim:Approval:TaskAlreadyHandled";

    /// <summary>当前用户不是该任务的处理人。</summary>
    public const string NotTaskAssignee = "Sim:Approval:NotTaskAssignee";

    /// <summary>业务类型未注册审批描述符。</summary>
    public const string BusinessTypeNotRegistered = "Sim:Approval:BusinessTypeNotRegistered";

    /// <summary>并发冲突，请刷新后重试。</summary>
    public const string ConcurrentModified = "Sim:Approval:ConcurrentModified";

    /// <summary>请求参数不合法。</summary>
    public const string InvalidRequest = "Sim:Approval:InvalidRequest";
}
