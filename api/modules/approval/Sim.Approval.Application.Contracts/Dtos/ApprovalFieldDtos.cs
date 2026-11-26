using System.ComponentModel.DataAnnotations;
using Sim.Approval.Domain.Shared;

namespace Sim.Approval.Application.Contracts.Dtos;

/// <summary>
/// 审批字段声明 —— 业务模块（或后续的表单引擎）按业务类型声明字段清单，
/// 既作为分支条件的可比较字段来源，也作为详情页只读渲染依据。
/// </summary>
public class ApprovalFieldDto
{
    /// <summary>字段标识（条件 JSON 中 <c>field</c> 的取值，同一业务类型内唯一）。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxFieldKeyLength)]
    public string Key { get; set; } = string.Empty;

    /// <summary>字段显示名（详情页与条件编辑器展示）。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxFieldDisplayNameLength)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>字段类型（取自 <see cref="ApprovalFieldType"/> 的取值名）。</summary>
    [Required]
    public string Type { get; set; } = nameof(ApprovalFieldType.String);

    /// <summary>枚举可选值（Type = Select 时提供）。</summary>
    public List<ApprovalFieldOptionDto> Options { get; set; } = [];

    /// <summary>发起时是否必填（缺失或为空的必填字段会被拒绝提交）。</summary>
    public bool Required { get; set; }

    /// <summary>输入提示（表单引擎与编辑器展示）。</summary>
    [MaxLength(ApprovalConsts.MaxFieldPlaceholderLength)]
    public string? Placeholder { get; set; }
}

/// <summary>枚举字段的可选值。</summary>
public class ApprovalFieldOptionDto
{
    /// <summary>选项值（实例快照中保存的值）。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxFieldValueLength)]
    public string Value { get; set; } = string.Empty;

    /// <summary>选项显示名。</summary>
    [MaxLength(ApprovalConsts.MaxFieldDisplayNameLength)]
    public string Label { get; set; } = string.Empty;
}

/// <summary>提交时的字段值（展示元数据以业务类型声明为准，客户端只提供取值）。</summary>
public class ApprovalFieldValueDto
{
    /// <summary>字段标识。</summary>
    [Required]
    [MaxLength(ApprovalConsts.MaxFieldKeyLength)]
    public string Key { get; set; } = string.Empty;

    /// <summary>字段取值（统一按字符串传输，按字段类型解释）。</summary>
    [MaxLength(ApprovalConsts.MaxFieldValueLength)]
    public string? Value { get; set; }
}

/// <summary>
/// 实例字段值快照项 —— 带标签的字段值，使详情页不依赖业务模块即可渲染。
/// </summary>
public class ApprovalContextFieldDto
{
    /// <summary>字段标识。</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>字段显示名（发起时按业务类型声明固化，业务描述符后续变更不回写历史实例）。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>字段类型（取自 <see cref="ApprovalFieldType"/> 的取值名）。</summary>
    public string Type { get; set; } = nameof(ApprovalFieldType.String);

    /// <summary>字段取值。</summary>
    public string? Value { get; set; }

    /// <summary>枚举可选值（发起时随快照固化；历史快照可能为空）。</summary>
    public List<ApprovalFieldOptionDto> Options { get; set; } = [];
}

/// <summary>实例业务上下文快照（<c>ApprovalInstance.ContextJson</c> 的持久化形态）。</summary>
public class ApprovalContextSnapshotDto
{
    /// <summary>字段值列表（按业务类型声明的顺序排列）。</summary>
    public List<ApprovalContextFieldDto> Fields { get; set; } = [];
}

/// <summary>
/// 审批附件：挂在实例或具体任务上，文件本体走框架既有存储链路。
/// 存储键与提供方不对外暴露，下载统一走审批模块的鉴权下载端点。
/// </summary>
public class ApprovalAttachmentDto
{
    /// <summary>附件记录 ID（下载端点使用）。</summary>
    public Guid Id { get; set; }

    /// <summary>所属实例 ID。</summary>
    public Guid InstanceId { get; set; }

    /// <summary>所属任务 ID（审批人上传时记录，实例级附件为空）。</summary>
    public Guid? TaskId { get; set; }

    /// <summary>原始文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>文件字节数。</summary>
    public long Size { get; set; }

    /// <summary>上传人。</summary>
    public Guid UploaderUserId { get; set; }

    /// <summary>上传时间。</summary>
    public DateTime CreationTime { get; set; }
}

/// <summary>审批选人目录项，仅包含选择与展示所需字段。</summary>
public class ApprovalUserOptionDto
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>审批组织选项，供组织负责人/组织人员来源选择组织；扁平列表按 parentId 组装树。</summary>
public class ApprovalOrganizationOptionDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }
}
