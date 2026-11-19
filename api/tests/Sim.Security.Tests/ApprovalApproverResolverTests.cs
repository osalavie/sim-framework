using Microsoft.Extensions.Logging.Abstractions;
using Sim.Approval.Application.Services;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Application.Contracts.Organization;

namespace Sim.Security.Tests;

/// <summary>
/// 审批人解析测试：组织负责人节点取所选组织的负责人，组织人员节点取所选组织的直接成员；
/// 解析为空时回落到流程兜底审批人，兜底也为空则抛出可诊断错误。
/// </summary>
public class ApprovalApproverResolverTests
{
    [Fact]
    public async Task Organization_manager_node_should_resolve_manager_of_selected_organization()
    {
        var organizationId = Guid.NewGuid();
        var manager = Guid.NewGuid();
        var node = CreateNode(ApprovalApproverType.OrganizationManager, organizationId);
        var instance = CreateInstance();

        var resolver = new ApprovalApproverResolver(
            new StubOrganizationDirectory { Manager = manager },
            NullLogger<ApprovalApproverResolver>.Instance);

        var resolved = await resolver.ResolveAsync(node, instance, fallbackApproverUserIds: null);

        Assert.Equal(manager, Assert.Single(resolved));
    }

    [Fact]
    public async Task Organization_manager_node_should_fall_back_when_organization_has_no_manager()
    {
        var fallback = Guid.NewGuid();
        var node = CreateNode(ApprovalApproverType.OrganizationManager, Guid.NewGuid());
        var instance = CreateInstance();

        var resolver = new ApprovalApproverResolver(
            new StubOrganizationDirectory { Manager = null },
            NullLogger<ApprovalApproverResolver>.Instance);

        var resolved = await resolver.ResolveAsync(
            node,
            instance,
            ApprovalRuntimeJson.SerializeUserIds([fallback]));

        Assert.Equal(fallback, Assert.Single(resolved));
    }

    [Fact]
    public async Task Organization_manager_node_should_fail_when_organization_missing_and_no_fallback()
    {
        var node = CreateNode(ApprovalApproverType.OrganizationManager, approverOrganizationId: null);
        var instance = CreateInstance();

        var resolver = new ApprovalApproverResolver(
            new StubOrganizationDirectory { Manager = Guid.NewGuid() },
            NullLogger<ApprovalApproverResolver>.Instance);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => resolver.ResolveAsync(node, instance, fallbackApproverUserIds: null));
        Assert.Equal(ApprovalErrorCodes.ApproverUnresolved, exception.Code);
    }

    [Fact]
    public async Task Organization_members_node_should_resolve_direct_members_of_selected_organization()
    {
        var organizationId = Guid.NewGuid();
        var members = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var node = CreateNode(ApprovalApproverType.OrganizationMembers, organizationId);
        var instance = CreateInstance();

        var resolver = new ApprovalApproverResolver(
            new StubOrganizationDirectory { Members = members },
            NullLogger<ApprovalApproverResolver>.Instance);

        var resolved = await resolver.ResolveAsync(node, instance, fallbackApproverUserIds: null);

        Assert.Equal(members, resolved);
    }

    [Fact]
    public async Task Organization_members_node_should_fail_when_organization_missing_and_no_fallback()
    {
        var node = CreateNode(ApprovalApproverType.OrganizationMembers, approverOrganizationId: null);
        var instance = CreateInstance();

        var resolver = new ApprovalApproverResolver(
            new StubOrganizationDirectory { Members = [Guid.NewGuid()] },
            NullLogger<ApprovalApproverResolver>.Instance);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => resolver.ResolveAsync(node, instance, fallbackApproverUserIds: null));
        Assert.Equal(ApprovalErrorCodes.ApproverUnresolved, exception.Code);
    }

    private static ApprovalFlowNode CreateNode(
        ApprovalApproverType approverType,
        Guid? approverOrganizationId = null) => new()
    {
        Id = Guid.NewGuid(),
        NodeKey = "n1",
        Name = "审批",
        NodeType = ApprovalNodeType.Approval,
        SortOrder = 1,
        ApproverType = approverType,
        ApproverOrganizationId = approverOrganizationId,
        MultiApproveMode = ApprovalMultiApproveMode.All
    };

    private static ApprovalInstance CreateInstance() => new()
    {
        Id = Guid.NewGuid(),
        BusinessType = ApprovalEngineFlowTests.TestBusinessType,
        BusinessId = "biz-1",
        Title = "测试实例",
        InitiatorUserId = Guid.NewGuid(),
        Status = ApprovalInstanceStatus.Running
    };

    private sealed class StubOrganizationDirectory : IOrganizationDirectory
    {
        public Guid? Manager { get; init; }

        public IReadOnlyList<Guid> Members { get; init; } = [];

        public Task<Guid?> FindPrimaryOrganizationUnitIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(Guid.NewGuid());

        public Task<IReadOnlyList<Guid>> FindManagerChainAsync(
            Guid userId,
            Guid? organizationUnitId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<Guid?> FindOrganizationManagerAsync(
            Guid organizationUnitId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Manager);

        public Task<IReadOnlyList<Guid>> FindDirectMemberUserIdsAsync(
            Guid organizationUnitId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Members);

        public Task<IReadOnlyList<OrganizationDirectoryUnit>> ListOrganizationUnitsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OrganizationDirectoryUnit>>([]);

        public Task<IReadOnlyList<Guid>> FindUserIdsByRoleNameAsync(
            string roleName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<bool> IsActiveUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != Guid.Empty);

        public Task<IReadOnlyList<OrganizationDirectoryUser>> SearchActiveUsersAsync(
            string? keyword,
            int maxResultCount = 20,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OrganizationDirectoryUser>>([]);
    }
}
