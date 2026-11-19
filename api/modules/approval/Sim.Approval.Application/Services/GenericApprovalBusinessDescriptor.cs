using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Application.Contracts.Ports;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 内建通用审批业务类型 — 供框架内自助发起与联调使用。
/// 业务模块接入时按 <see cref="IApprovalBusinessDescriptor"/> 注册各自的业务类型，
/// 编码默认值指向同名的已发布流程定义。
/// </summary>
public sealed class GenericApprovalBusinessDescriptor : IApprovalBusinessDescriptor
{
    /// <summary>通用业务类型标识，同时作为默认流程编码。</summary>
    public const string Type = "approval.generic";

    /// <inheritdoc />
    public string BusinessType => Type;

    /// <inheritdoc />
    public string DisplayName => "通用审批";

    /// <inheritdoc />
    public string? DefinitionCode => Type;

    /// <inheritdoc />
    public IReadOnlyList<ApprovalFieldDto> Fields => [];
}
