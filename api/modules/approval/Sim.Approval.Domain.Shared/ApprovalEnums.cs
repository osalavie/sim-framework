namespace Sim.Approval.Domain.Shared;

/// <summary>流程定义的发布状态。</summary>
public enum ApprovalFlowStatus
{
    /// <summary>草稿：可自由编辑，不能发起实例。</summary>
    Draft = 0,

    /// <summary>已发布：可发起实例，发布后内容不可修改。</summary>
    Published = 1,

    /// <summary>已停用：不可再发起新实例，进行中的实例不受影响。</summary>
    Disabled = 2
}

/// <summary>流程节点类型。</summary>
public enum ApprovalNodeType
{
    /// <summary>审批节点：产生待办任务。</summary>
    Approval = 0,

    /// <summary>抄送节点：不阻塞流程，仅生成抄送记录与通知。</summary>
    Cc = 1,

    /// <summary>开始节点：唯一入口，实例创建时激活；不生成任务。</summary>
    Start = 2,

    /// <summary>排他网关：按出边顺序取首个条件成立的出边，未选中的分支被跳过。</summary>
    ExclusiveGateway = 3,

    /// <summary>并行网关：fork 激活全部出边分支，join 等待全部入边到达。</summary>
    ParallelGateway = 4,

    /// <summary>结束节点：实例通过。</summary>
    End = 5
}

/// <summary>节点汇聚策略：决定多条入边时何时推进。</summary>
public enum ApprovalJoinPolicy
{
    /// <summary>任一路到达即推进（普通节点、排他网关、开始节点）。</summary>
    AnyArrival = 0,

    /// <summary>全部入边到达（或分支已死）才推进（并行 join、结束节点）。</summary>
    AllArrival = 1
}

/// <summary>实例内节点状态：并行汇合等待与死分支传播需要持久化。</summary>
public enum ApprovalNodeState
{
    /// <summary>未到达。</summary>
    Pending = 0,

    /// <summary>已到达且未完成（审批任务待处理）。</summary>
    Active = 1,

    /// <summary>已完成。</summary>
    Completed = 2,

    /// <summary>分支未选中，永不到达。</summary>
    Skipped = 3
}

/// <summary>
/// 审批字段类型：既决定详情页的只读渲染方式，也决定该字段能否作为分支条件
/// （<see cref="Attachment"/> 不可比较，不进入条件字段）。
/// </summary>
public enum ApprovalFieldType
{
    /// <summary>单行文本。</summary>
    String = 0,

    /// <summary>整数。</summary>
    Number = 1,

    /// <summary>小数（金额等）。</summary>
    Decimal = 2,

    /// <summary>日期。</summary>
    Date = 3,

    /// <summary>日期时间。</summary>
    DateTime = 4,

    /// <summary>布尔。</summary>
    Bool = 5,

    /// <summary>枚举选项（值取自 <c>Options</c>）。</summary>
    Select = 6,

    /// <summary>多行文本。</summary>
    Text = 7,

    /// <summary>附件（值不参与条件比较）。</summary>
    Attachment = 8
}

/// <summary>审批人来源。</summary>
public enum ApprovalApproverType
{
    /// <summary>指定用户。</summary>
    SpecifiedUsers = 0,

    /// <summary>按角色解析（角色下全部用户）。</summary>
    Role = 1,

    /// <summary>所选组织的负责人（按节点配置的组织解析）。</summary>
    OrganizationManager = 2,

    /// <summary>发起人本人。</summary>
    Initiator = 3,

    /// <summary>所选组织的直接成员（全部成员均可审批）。</summary>
    OrganizationMembers = 4
}

/// <summary>多人审批模式。</summary>
public enum ApprovalMultiApproveMode
{
    /// <summary>会签：全部审批人都通过后节点才通过，任一人拒绝则实例被拒。</summary>
    All = 0,

    /// <summary>或签：任一人通过即节点通过，其余待办自动取消；本轮全部拒绝时实例才被拒绝。</summary>
    Any = 1,

    /// <summary>顺序审批：按顺序依次生成待办，前一人通过后才生成下一人的待办。</summary>
    Sequential = 2
}

/// <summary>审批实例状态。</summary>
public enum ApprovalInstanceStatus
{
    /// <summary>进行中。</summary>
    Running = 0,

    /// <summary>已通过（终态）。</summary>
    Approved = 1,

    /// <summary>已拒绝（终态）。</summary>
    Rejected = 2,

    /// <summary>已退回发起人，等待重新提交。</summary>
    Returned = 3,

    /// <summary>已撤回（终态）。</summary>
    Withdrawn = 4,

    /// <summary>已终止（终态，管理端强制结束）。</summary>
    Terminated = 5
}

/// <summary>审批任务状态。</summary>
public enum ApprovalTaskStatus
{
    /// <summary>待处理。</summary>
    Pending = 0,

    /// <summary>已通过。</summary>
    Approved = 1,

    /// <summary>已拒绝。</summary>
    Rejected = 2,

    /// <summary>已跳过（或签他人通过、加签前任务被替换等）。</summary>
    Skipped = 3,

    /// <summary>已取消（撤回、终止、退回时未处理的任务）。</summary>
    Canceled = 4
}

/// <summary>审批任务类型。</summary>
public enum ApprovalTaskType
{
    /// <summary>节点正常审批任务。</summary>
    Approval = 0,

    /// <summary>前加签：加签人通过后才轮到当前处理人。</summary>
    AddSignBefore = 1,

    /// <summary>后加签：当前处理人通过后由加签人审批。</summary>
    AddSignAfter = 2,

    /// <summary>转交：原处理人转由他人处理。</summary>
    Transfer = 3
}

/// <summary>审批动作（处理记录）。</summary>
public enum ApprovalAction
{
    /// <summary>提交审批。</summary>
    Submit = 0,

    /// <summary>同意。</summary>
    Approve = 1,

    /// <summary>拒绝。</summary>
    Reject = 2,

    /// <summary>退回上一步。</summary>
    ReturnToPrevious = 3,

    /// <summary>退回发起人。</summary>
    ReturnToInitiator = 4,

    /// <summary>重新提交。</summary>
    Resubmit = 5,

    /// <summary>撤回。</summary>
    Withdraw = 6,

    /// <summary>终止。</summary>
    Terminate = 7,

    /// <summary>转交。</summary>
    Transfer = 8,

    /// <summary>加签。</summary>
    AddSign = 9,

    /// <summary>催办。</summary>
    Urge = 10,

    /// <summary>超时通知。</summary>
    TimeoutNotify = 11,

    /// <summary>发起人自动通过（审批人与发起人相同且方案开启自动通过）。</summary>
    AutoPass = 12,

    /// <summary>抄送。</summary>
    Cc = 13
}

/// <summary>加签位置。</summary>
public enum ApprovalAddSignPosition
{
    /// <summary>前加签：加签人先审批。</summary>
    Before = 0,

    /// <summary>后加签：当前处理人审批后由加签人审批。</summary>
    After = 1
}

/// <summary>状态扩展：终态判断等。</summary>
public static class ApprovalEnumExtensions
{
    /// <summary>是否为终态（终态实例不再产生待办）。</summary>
    public static bool IsTerminal(this ApprovalInstanceStatus status) =>
        status is ApprovalInstanceStatus.Approved
            or ApprovalInstanceStatus.Rejected
            or ApprovalInstanceStatus.Withdrawn
            or ApprovalInstanceStatus.Terminated;

    /// <summary>是否为未处理的待办任务。</summary>
    public static bool IsOpen(this ApprovalTaskStatus status) =>
        status == ApprovalTaskStatus.Pending;

    /// <summary>节点是否已进入不可再变化的状态（已完成或被跳过）。</summary>
    public static bool IsSettled(this ApprovalNodeState state) =>
        state is ApprovalNodeState.Completed or ApprovalNodeState.Skipped;

    /// <summary>节点类型是否可以承载审批人配置。</summary>
    public static bool IsApproval(this ApprovalNodeType nodeType) =>
        nodeType == ApprovalNodeType.Approval;

    /// <summary>字段类型是否可用于条件比较（附件无法比较）。</summary>
    public static bool IsConditionComparable(this ApprovalFieldType fieldType) =>
        fieldType != ApprovalFieldType.Attachment;
}
