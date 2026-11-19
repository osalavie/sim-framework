using System.ComponentModel.DataAnnotations;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Application.Contracts.Dtos;

namespace Sim.Approval.Application.Contracts.Dtos;

/// <summary>发起审批请求（业务模块服务端调用或用户端自助发起）。</summary>
public class SubmitApprovalDto
{
    /// <summary>流程编码；为空时按业务类型解析默认流程。</summary>
    [MaxLength(ApprovalConsts.MaxFlowCodeLength)]
    public string? DefinitionCode { get; set; }

    /// <summary>业务类型。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxBusinessTypeLength)]
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务对象标识。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxBusinessIdLength)]
    public string BusinessId { get; set; } = string.Empty;

    /// <summary>实例标题。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxTitleLength)]
    public string Title { get; set; } = string.Empty;

    /// <summary>业务上下文（条件求值与详情展示使用）。</summary>
    public Dictionary<string, string> Context { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// 字段值列表（表单引擎提交用，与 <see cref="Context"/> 两种形态均可；
    /// 同键以本属性为准，展示元数据取自业务类型声明）。
    /// </summary>
    public List<ApprovalFieldValueDto> Fields { get; set; } = [];
}

/// <summary>审批实例摘要（列表项）。</summary>
public class ApprovalInstanceListDto
{
    /// <summary>实例 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>实例编号。</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>流程编码。</summary>
    public string DefinitionCode { get; set; } = string.Empty;

    /// <summary>流程名称。</summary>
    public string DefinitionName { get; set; } = string.Empty;

    /// <summary>流程版本。</summary>
    public int DefinitionVersion { get; set; }

    /// <summary>业务类型。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务对象标识。</summary>
    public string BusinessId { get; set; } = string.Empty;

    /// <summary>实例标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>发起人用户 ID。</summary>
    public Guid InitiatorUserId { get; set; }

    /// <summary>实例状态：Running / Approved / Rejected / Returned / Withdrawn / Terminated。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>当前节点标识。</summary>
    public string? CurrentNodeKey { get; set; }

    /// <summary>当前节点名称。</summary>
    public string? CurrentNodeName { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>创建人标识。</summary>
    public Guid? CreatorId { get; set; }

    /// <summary>最后修改时间。</summary>
    public DateTime? LastModificationTime { get; set; }

    /// <summary>最后修改人标识。</summary>
    public Guid? LastModifierId { get; set; }

    /// <summary>结束时间。</summary>
    public DateTime? CompletedTime { get; set; }
}

/// <summary>审批实例明细（含节点进度、任务、记录与抄送）。</summary>
public class ApprovalInstanceDetailDto : ApprovalInstanceListDto
{
    /// <summary>发起人组织 ID。</summary>
    public Guid? InitiatorOrganizationUnitId { get; set; }

    /// <summary>业务上下文快照（带标签的字段值，旧实例为字段标识作标签）。</summary>
    public ApprovalContextSnapshotDto Context { get; set; } = new();

    /// <summary>实例附件（实例级与审批节点上传的合并列表）。</summary>
    public List<ApprovalAttachmentDto> Attachments { get; set; } = [];

    /// <summary>允许撤回（流程开关 + 状态 + 操作人）。</summary>
    public bool CanWithdraw { get; set; }

    /// <summary>允许重新提交（状态为已退回且操作人为发起人）。</summary>
    public bool CanResubmit { get; set; }

    /// <summary>允许上传实例级附件（发起人且实例在处理中）。</summary>
    public bool CanUploadAttachment { get; set; }

    /// <summary>节点进度。</summary>
    public List<ApprovalNodeProgressDto> Nodes { get; set; } = [];

    /// <summary>任务列表。</summary>
    public List<ApprovalTaskDto> Tasks { get; set; } = [];

    /// <summary>处理记录（按时间正序）。</summary>
    public List<ApprovalRecordDto> Records { get; set; } = [];

    /// <summary>抄送记录。</summary>
    public List<ApprovalCcDto> CcRecipients { get; set; } = [];
}

/// <summary>节点进度。</summary>
public class ApprovalNodeProgressDto
{
    /// <summary>节点标识。</summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>节点名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>节点类型。</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>节点顺序。</summary>
    public int SortOrder { get; set; }

    /// <summary>节点进度状态：NotStarted / Active / Approved / Rejected / Skipped / Canceled。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>节点状态：Pending / Active / Completed / Skipped（并行汇合与死分支判定结果）。</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>激活轮次（退回重提后递增）。</summary>
    public int Round { get; set; }

    /// <summary>该节点是否满足激活条件（未激活节点展示为 NotStarted）。</summary>
    public bool Activated { get; set; }

    /// <summary>节点任务。</summary>
    public List<ApprovalTaskDto> Tasks { get; set; } = [];
}

/// <summary>审批任务。</summary>
public class ApprovalTaskDto
{
    /// <summary>任务 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>节点标识。</summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>节点名称。</summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>节点顺序。</summary>
    public int NodeSort { get; set; }

    /// <summary>任务类型：Approval / AddSignBefore / AddSignAfter / Transfer。</summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>处理人。</summary>
    public Guid AssigneeUserId { get; set; }

    /// <summary>状态：Pending / Approved / Rejected / Skipped / Canceled。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>节点内次序。</summary>
    public int SortOrder { get; set; }

    /// <summary>处理意见。</summary>
    public string? Comment { get; set; }

    /// <summary>超时时间。</summary>
    public DateTime? DueAt { get; set; }

    /// <summary>处理时间。</summary>
    public DateTime? HandledTime { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>实例编号（待办/已办列表展示）。</summary>
    public string? InstanceNumber { get; set; }

    /// <summary>实例标题（待办/已办列表展示）。</summary>
    public string? InstanceTitle { get; set; }

    /// <summary>实例状态（待办/已办列表展示）。</summary>
    public string? InstanceStatus { get; set; }

    /// <summary>业务类型（待办/已办列表展示）。</summary>
    public string? BusinessType { get; set; }

    /// <summary>业务对象标识（业务详情跳转）。</summary>
    public string? BusinessId { get; set; }

    /// <summary>当前任务允许的操作（仅待办返回）。</summary>
    public ApprovalTaskPermissionsDto? Permissions { get; set; }
}

/// <summary>任务可用操作（由节点配置与流程开关决定）。</summary>
public class ApprovalTaskPermissionsDto
{
    /// <summary>可同意/拒绝。</summary>
    public bool CanApprove { get; set; }

    /// <summary>可转交。</summary>
    public bool CanTransfer { get; set; }

    /// <summary>可加签。</summary>
    public bool CanAddSign { get; set; }

    /// <summary>可退回上一步。</summary>
    public bool CanReturnToPrevious { get; set; }

    /// <summary>可退回发起人。</summary>
    public bool CanReturnToInitiator { get; set; }
}

/// <summary>处理记录。</summary>
public class ApprovalRecordDto
{
    /// <summary>记录 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>节点标识。</summary>
    public string? NodeKey { get; set; }

    /// <summary>节点名称。</summary>
    public string? NodeName { get; set; }

    /// <summary>动作发生前的节点标识（时间线 from→to 起点）。</summary>
    public string? FromNodeKey { get; set; }

    /// <summary>动作完成后的当前节点标识（并行为多个时以逗号分隔；实例结束时为空）。</summary>
    public string? ToNodeKey { get; set; }

    /// <summary>动作。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>操作人（系统动作如超时提醒为空）。</summary>
    public Guid? OperatorUserId { get; set; }

    /// <summary>目标用户。</summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>处理意见。</summary>
    public string? Comment { get; set; }

    /// <summary>发生时间。</summary>
    public DateTime CreationTime { get; set; }
}

/// <summary>抄送记录。</summary>
public class ApprovalCcDto
{
    /// <summary>抄送记录 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>节点标识。</summary>
    public string? NodeKey { get; set; }

    /// <summary>接收人。</summary>
    public Guid UserId { get; set; }

    /// <summary>已读时间。</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>实例编号（抄送列表展示）。</summary>
    public string? InstanceNumber { get; set; }

    /// <summary>实例标题（抄送列表展示）。</summary>
    public string? InstanceTitle { get; set; }

    /// <summary>实例状态（抄送列表展示）。</summary>
    public string? InstanceStatus { get; set; }
}

/// <summary>实例查询条件（我发起的 / 抄送我的 / 监控）。</summary>
public class ApprovalInstanceQueryDto : PagedAndSortedResultRequestDto
{
    /// <summary>状态过滤（Running / Approved / Rejected / Returned / Withdrawn / Terminated）。</summary>
    public string? Status { get; set; }

    /// <summary>业务类型过滤。</summary>
    public string? BusinessType { get; set; }

    /// <summary>流程编码过滤。</summary>
    public string? DefinitionCode { get; set; }

    /// <summary>发起人过滤（监控用）。</summary>
    public Guid? InitiatorUserId { get; set; }

    /// <summary>关键字（编号或标题模糊匹配）。</summary>
    public string? Keyword { get; set; }
}

/// <summary>待办 / 已办查询条件。</summary>
public class ApprovalTaskQueryDto : PagedAndSortedResultRequestDto
{
    /// <summary>流程编码过滤。</summary>
    public string? DefinitionCode { get; set; }

    /// <summary>业务类型过滤。</summary>
    public string? BusinessType { get; set; }

    /// <summary>关键字（编号或标题模糊匹配）。</summary>
    public string? Keyword { get; set; }
}

/// <summary>审批意见（同意 / 拒绝 / 退回 / 撤回 / 终止共用）。</summary>
public class ApprovalActionDto
{
    /// <summary>处理意见。</summary>
    [MaxLength(ApprovalConsts.MaxCommentLength)]
    public string? Comment { get; set; }
}

/// <summary>转交请求。</summary>
public class ApprovalTransferDto
{
    /// <summary>转交目标用户。</summary>
    [Required]
    public Guid TargetUserId { get; set; }

    /// <summary>处理意见。</summary>
    [MaxLength(ApprovalConsts.MaxCommentLength)]
    public string? Comment { get; set; }
}

/// <summary>加签请求。</summary>
public class ApprovalAddSignDto
{
    /// <summary>加签目标用户。</summary>
    [Required]
    public Guid TargetUserId { get; set; }

    /// <summary>加签位置：Before（前加签）/ After（后加签）。</summary>
    [Required]
    public string Position { get; set; } = nameof(ApprovalAddSignPosition.Before);

    /// <summary>处理意见。</summary>
    [MaxLength(ApprovalConsts.MaxCommentLength)]
    public string? Comment { get; set; }
}

/// <summary>终止请求。</summary>
public class ApprovalTerminateDto
{
    /// <summary>终止原因。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxCommentLength)]
    public string Comment { get; set; } = string.Empty;
}

/// <summary>实例计数（角标）。</summary>
public class ApprovalInstanceCountsDto
{
    /// <summary>待办数量。</summary>
    public int PendingCount { get; set; }

    /// <summary>未读抄送数量。</summary>
    public int UnreadCcCount { get; set; }

    /// <summary>我发起的进行中数量。</summary>
    public int SubmittedRunningCount { get; set; }
}
