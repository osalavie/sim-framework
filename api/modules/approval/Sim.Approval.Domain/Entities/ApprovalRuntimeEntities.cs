using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain;
using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;

namespace Sim.Approval.Domain.Entities;

/// <summary>
/// 审批实例。定义信息在发起时快照保存，后续流程定义变更不影响进行中的实例。
/// <see cref="RunningKey"/> 在实例处于活动状态时等于 <c>{BusinessType}:{BusinessId}</c>，
/// 用于保证同一业务对象同时只有一个进行中的实例；进入终态时置空（空值不参与唯一约束）。
/// 注：<see cref="AuditedAggregateRoot.TenantId"/> 在宿主（无租户）场景为空，空值不参与唯一约束，
/// 因此唯一索引使用始终有值的 <see cref="TenantScope"/>。
/// </summary>
[CodeFirst]
[SugarTable("approval_instance")]
[SugarIndex(ApprovalConsts.RunningKeyUniqueIndex, nameof(TenantScope), OrderByType.Asc,
    nameof(RunningKey), OrderByType.Asc, true)]
[SugarIndex("idx_approval_instance_initiator", nameof(TenantId), OrderByType.Asc,
    nameof(InitiatorUserId), OrderByType.Asc, nameof(Status), OrderByType.Asc)]
[SugarIndex("idx_approval_instance_business", nameof(TenantId), OrderByType.Asc,
    nameof(BusinessType), OrderByType.Asc, nameof(BusinessId), OrderByType.Asc)]
public sealed class ApprovalInstance : AuditedAggregateRoot
{
    /// <summary>租户范围（`host` 或租户 ID），唯一索引的有效组成部分。</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string TenantScope { get; set; } = "host";

    /// <summary>实例编号（业务编号生成器按 `approval.instance` 定义生成）。</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string Number { get; set; } = string.Empty;

    /// <summary>流程定义 ID（快照来源）。</summary>
    public Guid DefinitionId { get; set; }

    /// <summary>流程编码（快照）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxFlowCodeLength, IsNullable = false)]
    public string DefinitionCode { get; set; } = string.Empty;

    /// <summary>流程名称（快照）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxFlowNameLength, IsNullable = false)]
    public string DefinitionName { get; set; } = string.Empty;

    /// <summary>流程版本（快照）。</summary>
    public int DefinitionVersion { get; set; }

    /// <summary>业务类型。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxBusinessTypeLength, IsNullable = false)]
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务对象标识（由业务模块提供）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxBusinessIdLength, IsNullable = false)]
    public string BusinessId { get; set; } = string.Empty;

    /// <summary>实例标题（展示用）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxTitleLength, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    /// <summary>发起人用户 ID。</summary>
    public Guid InitiatorUserId { get; set; }

    /// <summary>发起人主组织 ID（快照，用于组织负责人解析与数据范围）。</summary>
    public Guid? InitiatorOrganizationUnitId { get; set; }

    /// <summary>实例状态。</summary>
    public ApprovalInstanceStatus Status { get; set; } = ApprovalInstanceStatus.Running;

    /// <summary>当前活动节点的顺序号（未激活任何节点时为 0）。</summary>
    public int CurrentNodeSort { get; set; }

    /// <summary>当前活动节点标识。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength, IsNullable = true)]
    public string? CurrentNodeKey { get; set; }

    /// <summary>当前活动节点名称（快照，列表展示用）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeNameLength, IsNullable = true)]
    public string? CurrentNodeName { get; set; }

    /// <summary>业务上下文（JSON 对象，提交/重提时由业务模块提供，用于条件求值与详情展示）。</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ContextJson { get; set; }

    /// <summary>活动实例唯一键（`{BusinessType}:{BusinessId}`），终态置空。</summary>
    [SugarColumn(Length = 300, IsNullable = true)]
    public string? RunningKey { get; set; }

    /// <summary>结束时间（进入终态或退回发起人时记录）。</summary>
    public DateTime? CompletedTime { get; set; }

    /// <summary>乐观并发版本号，每次状态变更递增。</summary>
    public int Version { get; set; }

    /// <summary>当前租户范围（`host` 或租户 ID），与在途唯一索引保持一致。</summary>
    public static string CurrentTenantScope() => TenantContext.CurrentTenantId?.ToString("N") ?? "host";
}

/// <summary>
/// 审批任务（待办）。审批人在任务创建时快照固化，节点配置变更不影响已创建的任务。
/// </summary>
[CodeFirst]
[SugarTable("approval_task")]
[SugarIndex("idx_approval_task_assignee", nameof(TenantId), OrderByType.Asc,
    nameof(AssigneeUserId), OrderByType.Asc, nameof(Status), OrderByType.Asc)]
[SugarIndex("idx_approval_task_instance", nameof(InstanceId), OrderByType.Asc, nameof(Status), OrderByType.Asc)]
[SugarIndex("idx_approval_task_due", nameof(Status), OrderByType.Asc, nameof(DueAt), OrderByType.Asc)]
public sealed class ApprovalTask : AuditedAggregateRoot
{
    /// <summary>所属实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>节点标识（快照）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength, IsNullable = false)]
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>节点名称（快照）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeNameLength, IsNullable = false)]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>节点顺序号（快照）。</summary>
    public int NodeSort { get; set; }

    /// <summary>任务类型。</summary>
    public ApprovalTaskType TaskType { get; set; } = ApprovalTaskType.Approval;

    /// <summary>当前处理人。</summary>
    public Guid AssigneeUserId { get; set; }

    /// <summary>转交/加签前的原处理人（转交与加签任务使用）。</summary>
    public Guid? OriginAssigneeUserId { get; set; }

    /// <summary>来源任务 ID（加签、转交由哪个任务派生）。</summary>
    public Guid? SourceTaskId { get; set; }

    /// <summary>任务状态。</summary>
    public ApprovalTaskStatus Status { get; set; } = ApprovalTaskStatus.Pending;

    /// <summary>同一节点内的任务次序（顺序审批与加签按此排序）。</summary>
    public int SortOrder { get; set; }

    /// <summary>节点激活轮次（从 1 开始；退回重提或回退到该节点后重新激活时递增，用于区分历史任务）。</summary>
    public int Round { get; set; } = 1;

    /// <summary>处理意见。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxCommentLength, IsNullable = true)]
    public string? Comment { get; set; }

    /// <summary>超时时间（按节点 TimeoutHours 计算）。</summary>
    public DateTime? DueAt { get; set; }

    /// <summary>处理时间。</summary>
    public DateTime? HandledTime { get; set; }

    /// <summary>最近一次超时通知时间（避免重复提醒）。</summary>
    public DateTime? TimeoutNotifiedAt { get; set; }
}

/// <summary>
/// 实例内节点状态。并行汇合的等待判定与死分支传播无法由任务推导，因此必须持久化：
/// 实例创建时按已发布定义写入全部节点（<see cref="ApprovalNodeState.Pending"/>），运行中只更新状态。
/// </summary>
[CodeFirst]
[SugarTable("approval_instance_node")]
[SugarIndex("idx_approval_instance_node_instance", nameof(InstanceId), OrderByType.Asc, nameof(NodeKey), OrderByType.Asc)]
public sealed class ApprovalInstanceNode : AuditedAggregateRoot
{
    /// <summary>所属实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>节点标识（与流程定义一致；线性定义合成的开始/结束节点使用内部标识）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength, IsNullable = false)]
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>节点名称（快照）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeNameLength, IsNullable = false)]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>节点类型（快照）。</summary>
    public ApprovalNodeType NodeType { get; set; } = ApprovalNodeType.Approval;

    /// <summary>节点顺序（快照，用于稳定排序）。</summary>
    public int SortOrder { get; set; }

    /// <summary>节点状态。</summary>
    public ApprovalNodeState State { get; set; } = ApprovalNodeState.Pending;

    /// <summary>激活时间（空表示尚未到达）。</summary>
    public DateTime? ActivatedTime { get; set; }

    /// <summary>完成时间（空表示尚未完成）。</summary>
    public DateTime? CompletedTime { get; set; }

    /// <summary>激活轮次（退回重提后重新激活时递增，与任务轮次一致）。</summary>
    public int Round { get; set; } = 1;
}

/// <summary>
/// 审批处理记录（审计流水）。创建后业务字段不再变化，仅 <see cref="ToNodeKey"/> 在推进完成后回填。
/// </summary>
[CodeFirst]
[SugarTable("approval_record")]
[SugarIndex("idx_approval_record_instance", nameof(InstanceId), OrderByType.Asc, nameof(CreationTime), OrderByType.Asc)]
public sealed class ApprovalRecord : CreationAuditedEntity
{
    /// <summary>所属实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>关联任务 ID（提交、撤回等实例级动作可为空）。</summary>
    public Guid? TaskId { get; set; }

    /// <summary>节点标识。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength, IsNullable = true)]
    public string? NodeKey { get; set; }

    /// <summary>节点名称。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeNameLength, IsNullable = true)]
    public string? NodeName { get; set; }

    /// <summary>动作发生前的节点标识（时间线的 from→to 起点）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength, IsNullable = true)]
    public string? FromNodeKey { get; set; }

    /// <summary>动作完成后的当前节点标识（并行为多个时以逗号分隔；实例结束时为空）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength * 4, IsNullable = true)]
    public string? ToNodeKey { get; set; }

    /// <summary>动作。</summary>
    public ApprovalAction Action { get; set; }

    /// <summary>操作人（系统动作如超时提醒为空）。</summary>
    public Guid? OperatorUserId { get; set; }

    /// <summary>目标用户（转交、加签、抄送的接收人）。</summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>处理意见。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxCommentLength, IsNullable = true)]
    public string? Comment { get; set; }
}

/// <summary>
/// 抄送记录。抄送不阻塞流程，接收人可标记已读。
/// </summary>
[CodeFirst]
[SugarTable("approval_cc")]
[SugarIndex("idx_approval_cc_user", nameof(TenantId), OrderByType.Asc, nameof(UserId), OrderByType.Asc)]
[SugarIndex("idx_approval_cc_instance", nameof(InstanceId), OrderByType.Asc)]
public sealed class ApprovalCc : AuditedEntity
{
    /// <summary>所属实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>抄送节点标识。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength, IsNullable = true)]
    public string? NodeKey { get; set; }

    /// <summary>接收人。</summary>
    public Guid UserId { get; set; }

    /// <summary>已读时间（空表示未读）。</summary>
    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// 审批附件。文件本体走框架既有存储链路（<c>IFileManagementService</c> 校验 + 落存储），
/// 审批模块只保存标识与元数据；<see cref="Provider"/> 与 <see cref="FileId"/> 一起用于解析下载地址。
/// </summary>
[CodeFirst]
[SugarTable("approval_attachment")]
[SugarIndex("idx_approval_attachment_instance", nameof(InstanceId), OrderByType.Asc, nameof(CreationTime), OrderByType.Asc)]
public sealed class ApprovalAttachment : CreationAuditedEntity
{
    /// <summary>所属实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>所属任务 ID（审批人上传时记录，实例级附件为空）。</summary>
    public Guid? TaskId { get; set; }

    /// <summary>文件标识（框架存储链路的 key）。</summary>
    [SugarColumn(Length = 512, IsNullable = false)]
    public string FileId { get; set; } = string.Empty;

    /// <summary>存储提供方（与 FileId 一起解析下载地址）。</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string Provider { get; set; } = string.Empty;

    /// <summary>原始文件名。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxAttachmentFileNameLength, IsNullable = false)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>文件字节数。</summary>
    public long Size { get; set; }

    /// <summary>上传人。</summary>
    public Guid UploaderUserId { get; set; }
}
