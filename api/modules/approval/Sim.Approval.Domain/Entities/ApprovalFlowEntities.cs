using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Entities;
using Sim.Framework.Domain;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;

namespace Sim.Approval.Domain.Entities;

/// <summary>
/// 审批流程定义（含节点）。同一 <see cref="Code"/> 可存在多个版本，发布后内容不可修改；
/// 修改流程需基于已发布版本创建新草稿版本。
/// </summary>
[CodeFirst]
[SugarTable("approval_flow_definition")]
[SugarIndex("uidx_approval_flow_definition_scope_version", nameof(TenantScope), OrderByType.Asc,
    nameof(Code), OrderByType.Asc, nameof(Version), OrderByType.Asc, true)]
[SugarIndex("idx_approval_flow_definition_business", nameof(TenantId), OrderByType.Asc,
    nameof(BusinessType), OrderByType.Asc)]
public sealed class ApprovalFlowDefinition : AuditedAggregateRoot
{
    /// <summary>非空租户范围，用于保证宿主与租户流程版本均可建立唯一约束。</summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string TenantScope { get; set; } = "host";

    public static string CurrentTenantScope() => TenantContext.CurrentTenantId?.ToString("N") ?? "host";

    /// <summary>流程编码（同一租户内唯一，跨版本复用）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxFlowCodeLength, IsNullable = false)]
    public string Code { get; set; } = string.Empty;

    /// <summary>流程名称。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxFlowNameLength, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>流程说明。</summary>
    [SugarColumn(Length = 1024, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>版本号，从 1 开始递增。</summary>
    public int Version { get; set; } = 1;

    /// <summary>发布状态。</summary>
    public ApprovalFlowStatus Status { get; set; } = ApprovalFlowStatus.Draft;

    /// <summary>绑定的业务类型（用于按业务类型解析默认流程，可空）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxBusinessTypeLength, IsNullable = true)]
    public string? BusinessType { get; set; }

    /// <summary>是否允许发起人在流程结束前撤回。</summary>
    public bool AllowWithdraw { get; set; } = true;

    /// <summary>撤回时间窗（小时，自发起时起算；空表示不限）。</summary>
    public int? WithdrawWindowHours { get; set; }

    /// <summary>审批人与发起人相同时是否自动通过该任务。</summary>
    public bool AutoPassWhenInitiator { get; set; }

    /// <summary>审批人解析失败时的兜底审批人（JSON 数组，Guid 列表）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxApproverJsonLength, IsNullable = true)]
    public string? FallbackApproverUserIds { get; set; }

    /// <summary>发布时间（空表示尚未发布）。</summary>
    public DateTime? PublishedTime { get; set; }

    /// <summary>创建该定义的来源版本 ID（基于历史版本调整时记录）。</summary>
    public Guid? BasedOnDefinitionId { get; set; }

    /// <summary>
    /// 画布布局（JSON：{ "node1": { "x": 120, "y": 80 } }）。
    /// 仅用于设计器还原，运行语义只依赖节点与连线，因此引擎不读取该字段。
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? LayoutJson { get; set; }
}

/// <summary>
/// 审批流程节点。节点按 <see cref="SortOrder"/> 顺序执行；抄送节点不阻塞流程。
/// 节点激活条件保存在 <see cref="ConditionJson"/>，为空表示无条件激活。
/// </summary>
[CodeFirst]
[SugarTable("approval_flow_node")]
[SugarIndex("idx_approval_flow_node_definition", nameof(DefinitionId), OrderByType.Asc, nameof(SortOrder), OrderByType.Asc)]
public sealed class ApprovalFlowNode : AuditedAggregateRoot
{
    /// <summary>所属流程定义 ID。</summary>
    public Guid DefinitionId { get; set; }

    /// <summary>节点标识（同一流程内唯一，条件表达式与会话记录引用该值）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength, IsNullable = false)]
    public string NodeKey { get; set; } = string.Empty;

    /// <summary>节点名称。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeNameLength, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>节点类型：审批或抄送。</summary>
    public ApprovalNodeType NodeType { get; set; } = ApprovalNodeType.Approval;

    /// <summary>节点顺序（从 1 开始）。</summary>
    public int SortOrder { get; set; }

    /// <summary>审批人来源。</summary>
    public ApprovalApproverType ApproverType { get; set; } = ApprovalApproverType.SpecifiedUsers;

    /// <summary>指定审批人（JSON 数组，Guid 列表；ApproverType = SpecifiedUsers 时生效）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxApproverJsonLength, IsNullable = true)]
    public string? ApproverUserIds { get; set; }

    /// <summary>审批角色名（ApproverType = Role 时生效）。</summary>
    [SugarColumn(Length = 128, IsNullable = true)]
    public string? ApproverRoleName { get; set; }

    /// <summary>
    /// 审批组织（ApproverType = OrganizationManager / OrganizationMembers 时生效）：
    /// OrganizationManager 解析该组织负责人，OrganizationMembers 解析该组织直接成员。
    /// </summary>
    public Guid? ApproverOrganizationId { get; set; }

    /// <summary>多人审批模式（会签 / 或签 / 顺序）。</summary>
    public ApprovalMultiApproveMode MultiApproveMode { get; set; } = ApprovalMultiApproveMode.All;

    /// <summary>节点激活条件：为空表示无条件激活。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxConditionJsonLength, IsNullable = true)]
    public string? ConditionJson { get; set; }

    /// <summary>是否允许处理人转交。</summary>
    public bool AllowTransfer { get; set; } = true;

    /// <summary>是否允许处理人加签。</summary>
    public bool AllowAddSign { get; set; } = true;

    /// <summary>是否允许处理人退回发起人。</summary>
    public bool AllowReturnToInitiator { get; set; } = true;

    /// <summary>超时小时数（空表示不限制；超时仅通知，不自动通过）。</summary>
    public int? TimeoutHours { get; set; }

    /// <summary>抄送配置（JSON：{ "userIds": [...], "roles": [...], "initiator": bool }）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxCcConfigJsonLength, IsNullable = true)]
    public string? CcConfigJson { get; set; }

    /// <summary>汇聚策略：多入边时的推进判定（并行 join 与结束节点用 AllArrival）。</summary>
    public ApprovalJoinPolicy JoinPolicy { get; set; } = ApprovalJoinPolicy.AnyArrival;
}

/// <summary>
/// 流程连线（有向图的边）。条件出边按 <see cref="SortOrder"/> 取首个成立项；
/// 无条件出边是默认出边，仅在所有条件出边均不成立时选中，与排序无关。
/// </summary>
[CodeFirst]
[SugarTable("approval_flow_edge")]
[SugarIndex("idx_approval_flow_edge_definition", nameof(DefinitionId), OrderByType.Asc, nameof(SortOrder), OrderByType.Asc)]
[SugarIndex("idx_approval_flow_edge_from", nameof(DefinitionId), OrderByType.Asc,
    nameof(FromNodeKey), OrderByType.Asc)]
public sealed class ApprovalFlowEdge : AuditedAggregateRoot
{
    /// <summary>所属流程定义 ID。</summary>
    public Guid DefinitionId { get; set; }

    /// <summary>起点节点标识。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength, IsNullable = false)]
    public string FromNodeKey { get; set; } = string.Empty;

    /// <summary>终点节点标识。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxNodeKeyLength, IsNullable = false)]
    public string ToNodeKey { get; set; } = string.Empty;

    /// <summary>出边条件 JSON；为空表示默认出边。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxConditionJsonLength, IsNullable = true)]
    public string? ConditionJson { get; set; }

    /// <summary>连线标签（仅画布展示）。</summary>
    [SugarColumn(Length = ApprovalConsts.MaxEdgeLabelLength, IsNullable = true)]
    public string? Label { get; set; }

    /// <summary>同一起点多条出边时的求值顺序（从 1 开始）。</summary>
    public int SortOrder { get; set; }
}
