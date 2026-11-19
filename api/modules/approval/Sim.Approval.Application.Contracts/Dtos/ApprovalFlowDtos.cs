using System.ComponentModel.DataAnnotations;
using Sim.Approval.Domain.Shared;

namespace Sim.Approval.Application.Contracts.Dtos;

/// <summary>流程定义列表项（不含节点明细）。</summary>
public class ApprovalFlowDefinitionListDto
{
    /// <summary>流程定义 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>流程编码（同一编码的多个版本共享）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>流程名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>流程说明。</summary>
    public string? Description { get; set; }

    /// <summary>版本号。</summary>
    public int Version { get; set; }

    /// <summary>发布状态：Draft / Published / Disabled。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>绑定业务类型。</summary>
    public string? BusinessType { get; set; }

    /// <summary>节点数量。</summary>
    public int NodeCount { get; set; }

    /// <summary>发布时间。</summary>
    public DateTime? PublishedTime { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>创建人标识。</summary>
    public Guid? CreatorId { get; set; }

    /// <summary>最后修改时间。</summary>
    public DateTime? LastModificationTime { get; set; }

    /// <summary>最后修改人标识。</summary>
    public Guid? LastModifierId { get; set; }
}

/// <summary>流程定义明细（含节点）。</summary>
public class ApprovalFlowDefinitionDto : ApprovalFlowDefinitionListDto
{
    /// <summary>是否允许撤回。</summary>
    public bool AllowWithdraw { get; set; } = true;

    /// <summary>撤回时间窗（小时，自发起起算；空表示不限）。</summary>
    public int? WithdrawWindowHours { get; set; }

    /// <summary>审批人与发起人相同时是否自动通过。</summary>
    public bool AutoPassWhenInitiator { get; set; }

    /// <summary>兜底审批人。</summary>
    public List<Guid> FallbackApproverUserIds { get; set; } = [];

    /// <summary>来源版本 ID。</summary>
    public Guid? BasedOnDefinitionId { get; set; }

    /// <summary>节点列表（按顺序）。</summary>
    public List<ApprovalFlowNodeDto> Nodes { get; set; } = [];

    /// <summary>连线列表（有向图的边）。</summary>
    public List<ApprovalFlowEdgeDto> Edges { get; set; } = [];

    /// <summary>画布布局 JSON（仅设计器还原用，引擎不读取）。</summary>
    public string? LayoutJson { get; set; }
}

/// <summary>流程节点。</summary>
public class ApprovalFlowNodeDto
{
    /// <summary>节点 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>节点标识（流程内唯一）。</summary>
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>节点名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>节点类型：Approval / Cc / Start / ExclusiveGateway / ParallelGateway / End。</summary>
    public string NodeType { get; set; } = string.Empty;

    /// <summary>节点顺序。</summary>
    public int SortOrder { get; set; }

    /// <summary>审批人来源：SpecifiedUsers / Role / OrganizationManager / OrganizationMembers / Initiator。</summary>
    public string ApproverType { get; set; } = string.Empty;

    /// <summary>指定审批人。</summary>
    public List<Guid> ApproverUserIds { get; set; } = [];

    /// <summary>审批角色名。</summary>
    public string? ApproverRoleName { get; set; }

    /// <summary>审批组织（ApproverType = OrganizationManager / OrganizationMembers 时生效）。</summary>
    public Guid? ApproverOrganizationId { get; set; }

    /// <summary>多人审批模式：All（会签）/ Any（或签）/ Sequential（顺序）。</summary>
    public string MultiApproveMode { get; set; } = string.Empty;

    /// <summary>节点激活条件 JSON。</summary>
    public string? ConditionJson { get; set; }

    /// <summary>是否允许转交。</summary>
    public bool AllowTransfer { get; set; }

    /// <summary>是否允许加签。</summary>
    public bool AllowAddSign { get; set; }

    /// <summary>是否允许退回发起人。</summary>
    public bool AllowReturnToInitiator { get; set; }

    /// <summary>超时小时数。</summary>
    public int? TimeoutHours { get; set; }

    /// <summary>抄送配置 JSON。</summary>
    public string? CcConfigJson { get; set; }

    /// <summary>汇聚策略：AnyArrival（任一路到达）/ AllArrival（全部入边到达）。</summary>
    public string JoinPolicy { get; set; } = string.Empty;
}

/// <summary>流程连线（有向图的边）。</summary>
public class ApprovalFlowEdgeDto
{
    /// <summary>连线 ID。</summary>
    public Guid Id { get; set; }

    /// <summary>起点节点标识。</summary>
    public string FromNodeKey { get; set; } = string.Empty;

    /// <summary>终点节点标识。</summary>
    public string ToNodeKey { get; set; } = string.Empty;

    /// <summary>出边条件 JSON（为空表示默认出边）。</summary>
    public string? ConditionJson { get; set; }

    /// <summary>连线标签（仅画布展示）。</summary>
    public string? Label { get; set; }

    /// <summary>同一起点多条出边时的求值顺序。</summary>
    public int SortOrder { get; set; }
}

/// <summary>保存流程节点（创建与更新草稿共用）。</summary>
public class SaveApprovalFlowNodeDto
{
    /// <summary>节点标识（流程内唯一，字母数字与中划线，条件表达式引用该值）。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxNodeKeyLength)]
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>节点名称。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxNodeNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>节点类型（Approval / Cc / Start / ExclusiveGateway / ParallelGateway / End）。</summary>
    [Required]
    public string NodeType { get; set; } = nameof(ApprovalNodeType.Approval);

    /// <summary>节点顺序（从 1 开始，审批节点按该值执行）。</summary>
    public int SortOrder { get; set; }

    /// <summary>审批人来源（SpecifiedUsers / Role / OrganizationManager / OrganizationMembers / Initiator）。</summary>
    [Required]
    public string ApproverType { get; set; } = nameof(ApprovalApproverType.SpecifiedUsers);

    /// <summary>指定审批人（ApproverType = SpecifiedUsers 时必填）。</summary>
    public List<Guid> ApproverUserIds { get; set; } = [];

    /// <summary>审批角色名（ApproverType = Role 时必填）。</summary>
    [MaxLength(128)]
    public string? ApproverRoleName { get; set; }

    /// <summary>审批组织（ApproverType = OrganizationManager / OrganizationMembers 时必填）。</summary>
    public Guid? ApproverOrganizationId { get; set; }

    /// <summary>多人审批模式（All / Any / Sequential）。</summary>
    [Required]
    public string MultiApproveMode { get; set; } = nameof(ApprovalMultiApproveMode.All);

    /// <summary>节点激活条件 JSON（可空）。</summary>
    [MaxLength(ApprovalConsts.MaxConditionJsonLength)]
    public string? ConditionJson { get; set; }

    /// <summary>是否允许转交。</summary>
    public bool AllowTransfer { get; set; } = true;

    /// <summary>是否允许加签。</summary>
    public bool AllowAddSign { get; set; } = true;

    /// <summary>是否允许退回发起人。</summary>
    public bool AllowReturnToInitiator { get; set; } = true;

    /// <summary>超时小时数（1-8760，空表示不限制）。</summary>
    [Range(1, 8760)]
    public int? TimeoutHours { get; set; }

    /// <summary>抄送配置 JSON（NodeType = Cc 时使用）。</summary>
    [MaxLength(ApprovalConsts.MaxCcConfigJsonLength)]
    public string? CcConfigJson { get; set; }

    /// <summary>汇聚策略（AnyArrival / AllArrival；并行 join 用 AllArrival）。</summary>
    [Required]
    public string JoinPolicy { get; set; } = nameof(ApprovalJoinPolicy.AnyArrival);
}

/// <summary>保存流程连线（创建与更新草稿共用，整体覆盖）。</summary>
public class SaveApprovalFlowEdgeDto
{
    /// <summary>起点节点标识。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxNodeKeyLength)]
    public string FromNodeKey { get; set; } = string.Empty;

    /// <summary>终点节点标识。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxNodeKeyLength)]
    public string ToNodeKey { get; set; } = string.Empty;

    /// <summary>出边条件 JSON（为空表示默认出边）。</summary>
    [MaxLength(ApprovalConsts.MaxConditionJsonLength)]
    public string? ConditionJson { get; set; }

    /// <summary>连线标签（仅画布展示）。</summary>
    [MaxLength(ApprovalConsts.MaxEdgeLabelLength)]
    public string? Label { get; set; }
}

/// <summary>创建流程定义（生成草稿版本）。</summary>
public class CreateApprovalFlowDefinitionDto
{
    /// <summary>流程编码（租户内唯一，字母数字与中划线）。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxFlowCodeLength)]
    public string Code { get; set; } = string.Empty;

    /// <summary>流程名称。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxFlowNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>流程说明。</summary>
    [MaxLength(1024)]
    public string? Description { get; set; }

    /// <summary>绑定业务类型。</summary>
    [MaxLength(ApprovalConsts.MaxBusinessTypeLength)]
    public string? BusinessType { get; set; }

    /// <summary>是否允许撤回。</summary>
    public bool AllowWithdraw { get; set; } = true;

    /// <summary>撤回时间窗（小时，自发起起算；空表示不限）。</summary>
    [Range(1, 8760)]
    public int? WithdrawWindowHours { get; set; }

    /// <summary>审批人与发起人相同时是否自动通过。</summary>
    public bool AutoPassWhenInitiator { get; set; }

    /// <summary>兜底审批人。</summary>
    public List<Guid> FallbackApproverUserIds { get; set; } = [];

    /// <summary>节点列表。</summary>
    public List<SaveApprovalFlowNodeDto> Nodes { get; set; } = [];

    /// <summary>连线列表（为空时按节点顺序视为线性流程）。</summary>
    public List<SaveApprovalFlowEdgeDto> Edges { get; set; } = [];

    /// <summary>画布布局 JSON（仅设计器还原用）。</summary>
    [MaxLength(ApprovalConsts.MaxLayoutJsonLength)]
    public string? LayoutJson { get; set; }
}

/// <summary>更新流程定义（仅草稿状态可更新，节点整体覆盖）。</summary>
public class UpdateApprovalFlowDefinitionDto
{
    /// <summary>流程名称。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxFlowNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>流程说明。</summary>
    [MaxLength(1024)]
    public string? Description { get; set; }

    /// <summary>绑定业务类型。</summary>
    [MaxLength(ApprovalConsts.MaxBusinessTypeLength)]
    public string? BusinessType { get; set; }

    /// <summary>是否允许撤回。</summary>
    public bool AllowWithdraw { get; set; } = true;

    /// <summary>撤回时间窗（小时，自发起起算；空表示不限）。</summary>
    [Range(1, 8760)]
    public int? WithdrawWindowHours { get; set; }

    /// <summary>审批人与发起人相同时是否自动通过。</summary>
    public bool AutoPassWhenInitiator { get; set; }

    /// <summary>兜底审批人。</summary>
    public List<Guid> FallbackApproverUserIds { get; set; } = [];

    /// <summary>节点列表。</summary>
    public List<SaveApprovalFlowNodeDto> Nodes { get; set; } = [];

    /// <summary>连线列表（为空时按节点顺序视为线性流程）。</summary>
    public List<SaveApprovalFlowEdgeDto> Edges { get; set; } = [];

    /// <summary>画布布局 JSON（仅设计器还原用）。</summary>
    [MaxLength(ApprovalConsts.MaxLayoutJsonLength)]
    public string? LayoutJson { get; set; }
}

/// <summary>发布前校验结果（设计器展示逐条问题，发布判定以此为准）。</summary>
public class ApprovalFlowValidationResultDto
{
    /// <summary>是否通过（问题清单为空）。</summary>
    public bool Valid { get; set; }

    /// <summary>逐条问题（可读文案，与前端即时提示共用同一套规则）。</summary>
    public List<string> Problems { get; set; } = [];
}

/// <summary>业务类型（由业务模块的审批描述符注册）。</summary>
public class ApprovalBusinessTypeDto
{
    /// <summary>业务类型标识。</summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>业务类型显示名。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>默认流程编码。</summary>
    public string? DefinitionCode { get; set; }

    /// <summary>
    /// 业务字段清单（详情页只读渲染与分支条件字段来源）；
    /// 条件编辑器应过滤掉 <see cref="ApprovalFieldType.Attachment"/> 类型的字段。
    /// </summary>
    public List<ApprovalFieldDto> Fields { get; set; } = [];
}
