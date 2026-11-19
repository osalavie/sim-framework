using Microsoft.Extensions.Logging;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.Organization;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 审批人解析器 —— 按节点配置解析出实际审批人。
/// 组织负责人 / 组织人员按节点配置的组织解析；解析结果为空时使用流程兜底审批人。
/// </summary>
public sealed class ApprovalApproverResolver(
    IOrganizationDirectory organizationDirectory,
    ILogger<ApprovalApproverResolver> logger)
{
    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        ApprovalFlowNode node,
        ApprovalInstance instance,
        string? fallbackApproverUserIds,
        CancellationToken cancellationToken = default)
    {
        var resolved = node.ApproverType switch
        {
            ApprovalApproverType.SpecifiedUsers => ApprovalRuntimeJson.DeserializeUserIds(node.ApproverUserIds),
            ApprovalApproverType.Role => await organizationDirectory.FindUserIdsByRoleNameAsync(
                node.ApproverRoleName ?? string.Empty,
                cancellationToken),
            ApprovalApproverType.OrganizationManager => await ResolveOrganizationManagerAsync(node, cancellationToken),
            ApprovalApproverType.OrganizationMembers => await ResolveOrganizationMembersAsync(node, cancellationToken),
            ApprovalApproverType.Initiator => [instance.InitiatorUserId],
            _ => []
        };

        var assignees = resolved.Where(id => id != Guid.Empty).Distinct().ToList();
        if (assignees.Count == 0)
        {
            assignees = ApprovalRuntimeJson.DeserializeUserIds(fallbackApproverUserIds).ToList();
            if (assignees.Count > 0)
            {
                logger.LogWarning(
                    "节点 [{NodeKey}] 未解析出审批人，改用流程兜底审批人（共 {Count} 人）",
                    node.NodeKey,
                    assignees.Count);
            }
        }

        if (assignees.Count == 0)
        {
            throw new BusinessException(
                ApprovalErrorCodes.ApproverUnresolved,
                $"节点 [{node.Name}] 未解析出任何审批人，请检查节点配置或补充兜底审批人。");
        }

        return assignees;
    }

    /// <summary>取节点配置组织的负责人；未配置组织或未设负责人时不解析（交由兜底审批人处理）。</summary>
    private async Task<IReadOnlyList<Guid>> ResolveOrganizationManagerAsync(
        ApprovalFlowNode node,
        CancellationToken cancellationToken)
    {
        if (node.ApproverOrganizationId is not { } organizationUnitId)
        {
            return [];
        }

        var manager = await organizationDirectory.FindOrganizationManagerAsync(organizationUnitId, cancellationToken);
        return manager is { } userId ? [userId] : [];
    }

    /// <summary>取节点配置组织的直接成员；未配置组织时不解析（交由兜底审批人处理）。</summary>
    private async Task<IReadOnlyList<Guid>> ResolveOrganizationMembersAsync(
        ApprovalFlowNode node,
        CancellationToken cancellationToken)
    {
        return node.ApproverOrganizationId is { } organizationUnitId
            ? await organizationDirectory.FindDirectMemberUserIdsAsync(organizationUnitId, cancellationToken)
            : [];
    }
}
