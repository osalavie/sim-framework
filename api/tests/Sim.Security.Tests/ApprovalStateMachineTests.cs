using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Runtime;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Security.Tests;

/// <summary>
/// 审批状态机守卫测试：实例与任务的可操作性判定必须抛出具名业务错误码。
/// </summary>
public class ApprovalStateMachineTests
{
    private static ApprovalInstance CreateInstance(
        ApprovalInstanceStatus status = ApprovalInstanceStatus.Running,
        Guid? initiatorUserId = null) => new()
    {
        Id = Guid.NewGuid(),
        InitiatorUserId = initiatorUserId ?? Guid.NewGuid(),
        Status = status
    };

    [Fact]
    public void EnsureRunning_should_reject_terminal_instance()
    {
        var instance = CreateInstance(ApprovalInstanceStatus.Approved);
        var exception = Assert.Throws<BusinessException>(() => ApprovalStateMachine.EnsureRunning(instance));
        Assert.Equal(ApprovalErrorCodes.InstanceNotRunning, exception.Code);
    }

    [Fact]
    public void EnsureReturned_should_only_accept_returned_instance()
    {
        ApprovalStateMachine.EnsureReturned(CreateInstance(ApprovalInstanceStatus.Returned));
        var exception = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureReturned(CreateInstance(ApprovalInstanceStatus.Running)));
        Assert.Equal(ApprovalErrorCodes.InstanceNotReturned, exception.Code);
    }

    [Fact]
    public void EnsureInitiator_should_reject_other_user()
    {
        var initiator = Guid.NewGuid();
        var instance = CreateInstance(initiatorUserId: initiator);
        ApprovalStateMachine.EnsureInitiator(instance, initiator);

        var exception = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureInitiator(instance, Guid.NewGuid()));
        Assert.Equal(ApprovalErrorCodes.NotInitiator, exception.Code);
    }

    [Theory]
    [InlineData(true, ApprovalInstanceStatus.Running, true)]
    [InlineData(false, ApprovalInstanceStatus.Running, false)]
    [InlineData(true, ApprovalInstanceStatus.Withdrawn, false)]
    [InlineData(true, ApprovalInstanceStatus.Returned, true)]
    public void EnsureCanWithdraw_should_require_initiator_running_state_and_flow_flag(
        bool allowWithdraw,
        ApprovalInstanceStatus status,
        bool expectedToPass)
    {
        var initiator = Guid.NewGuid();
        var instance = CreateInstance(status, initiator);
        var definition = new ApprovalFlowDefinition { Id = Guid.NewGuid(), AllowWithdraw = allowWithdraw };

        if (expectedToPass)
        {
            ApprovalStateMachine.EnsureCanWithdraw(instance, definition, initiator);
            return;
        }

        Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureCanWithdraw(instance, definition, initiator));
    }

    [Fact]
    public void EnsureCanWithdraw_should_reject_expired_withdraw_window()
    {
        var initiator = Guid.NewGuid();
        var definition = new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(),
            AllowWithdraw = true,
            WithdrawWindowHours = 2
        };

        var withinWindow = CreateInstance(ApprovalInstanceStatus.Running, initiator);
        withinWindow.CreationTime = DateTime.Now.AddHours(-1);
        ApprovalStateMachine.EnsureCanWithdraw(withinWindow, definition, initiator);

        var expired = CreateInstance(ApprovalInstanceStatus.Running, initiator);
        expired.CreationTime = DateTime.Now.AddHours(-3);
        var exception = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureCanWithdraw(expired, definition, initiator));
        Assert.Equal(ApprovalErrorCodes.WithdrawWindowExpired, exception.Code);
    }

    [Theory]
    [InlineData(ApprovalInstanceStatus.Running, true)]
    [InlineData(ApprovalInstanceStatus.Returned, true)]
    [InlineData(ApprovalInstanceStatus.Withdrawn, false)]
    [InlineData(ApprovalInstanceStatus.Approved, false)]
    public void CanWithdraw_should_match_ensure_guard(ApprovalInstanceStatus status, bool expected)
    {
        var initiator = Guid.NewGuid();
        var instance = CreateInstance(status, initiator);
        instance.CreationTime = DateTime.Now;
        var definition = new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(),
            AllowWithdraw = true,
            WithdrawWindowHours = 1
        };

        Assert.Equal(expected, ApprovalStateMachine.CanWithdraw(instance, definition, initiator));
        Assert.False(ApprovalStateMachine.CanWithdraw(instance, definition, Guid.NewGuid()));
    }

    [Fact]
    public void CanWithdraw_should_match_ensure_guard_on_expired_window()
    {
        var initiator = Guid.NewGuid();
        var instance = CreateInstance(ApprovalInstanceStatus.Running, initiator);
        instance.CreationTime = DateTime.Now.AddHours(-5);
        var definition = new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(),
            AllowWithdraw = true,
            WithdrawWindowHours = 2
        };

        Assert.False(ApprovalStateMachine.CanWithdraw(instance, definition, initiator));
        var exception = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureCanWithdraw(instance, definition, initiator));
        Assert.Equal(ApprovalErrorCodes.WithdrawWindowExpired, exception.Code);
    }

    [Fact]
    public void EnsureCanWithdraw_should_reject_non_initiator()
    {
        var instance = CreateInstance(initiatorUserId: Guid.NewGuid());
        var definition = new ApprovalFlowDefinition { Id = Guid.NewGuid(), AllowWithdraw = true };

        var exception = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureCanWithdraw(instance, definition, Guid.NewGuid()));
        Assert.Equal(ApprovalErrorCodes.NotInitiator, exception.Code);
    }

    [Fact]
    public void EnsureTaskActionable_should_reject_handled_task_and_other_assignee()
    {
        var assignee = Guid.NewGuid();
        var handled = new ApprovalTask { Id = Guid.NewGuid(), AssigneeUserId = assignee, Status = ApprovalTaskStatus.Approved };
        var exception = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureTaskActionable(handled, assignee));
        Assert.Equal(ApprovalErrorCodes.TaskAlreadyHandled, exception.Code);

        var pending = new ApprovalTask { Id = Guid.NewGuid(), AssigneeUserId = assignee, Status = ApprovalTaskStatus.Pending };
        var notAssignee = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureTaskActionable(pending, Guid.NewGuid()));
        Assert.Equal(ApprovalErrorCodes.NotTaskAssignee, notAssignee.Code);
    }

    [Theory]
    [InlineData("transfer", ApprovalErrorCodes.TransferNotAllowed)]
    [InlineData("add-sign", ApprovalErrorCodes.AddSignNotAllowed)]
    [InlineData("return", ApprovalErrorCodes.ReturnNotAllowed)]
    public void EnsureNodeAllows_should_map_feature_to_error_code(string feature, string expectedCode)
    {
        var node = new ApprovalFlowNode { Id = Guid.NewGuid(), NodeKey = "n1", Name = "节点" };
        var exception = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureNodeAllows(node, feature, allowed: false));
        Assert.Equal(expectedCode, exception.Code);

        ApprovalStateMachine.EnsureNodeAllows(node, feature, allowed: true);
    }

    [Fact]
    public void EnsureValidTarget_should_reject_empty_and_self_target()
    {
        var operatorUserId = Guid.NewGuid();

        var empty = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureValidTarget(operatorUserId, Guid.Empty));
        Assert.Equal(ApprovalErrorCodes.InvalidTargetUser, empty.Code);

        var self = Assert.Throws<BusinessException>(() =>
            ApprovalStateMachine.EnsureValidTarget(operatorUserId, operatorUserId));
        Assert.Equal(ApprovalErrorCodes.InvalidTargetUser, self.Code);

        ApprovalStateMachine.EnsureValidTarget(operatorUserId, Guid.NewGuid());
    }

    [Fact]
    public void Terminal_and_open_status_helpers_should_match_state_semantics()
    {
        Assert.True(ApprovalInstanceStatus.Approved.IsTerminal());
        Assert.True(ApprovalInstanceStatus.Rejected.IsTerminal());
        Assert.True(ApprovalInstanceStatus.Withdrawn.IsTerminal());
        Assert.True(ApprovalInstanceStatus.Terminated.IsTerminal());
        Assert.False(ApprovalInstanceStatus.Running.IsTerminal());
        Assert.False(ApprovalInstanceStatus.Returned.IsTerminal());

        Assert.True(ApprovalTaskStatus.Pending.IsOpen());
        Assert.False(ApprovalTaskStatus.Skipped.IsOpen());
    }
}
