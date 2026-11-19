using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Application.Services;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.IRepositories;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Application.Contracts.Content;
using Sim.Framework.Application.Content;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.ScheduleTask;
using Sim.System.Domain.Numbering;
using Sim.System.Domain.User;

namespace Sim.Security.Tests;

/// <summary>
/// 审批引擎端到端测试：直接通过依赖注入解析引擎，验证节点推进、条件跳过、
/// 或签、抄送、退回重提与撤回对实例状态和待办任务的真实影响。
/// </summary>
[Collection(HostCollection.Name)]
public sealed class ApprovalEngineFlowTests(HostFixture fixture) : IClassFixture<HostFixture>
{
    /// <summary>测试用业务类型，对应 <see cref="TestApprovalBusinessDescriptor"/>。</summary>
    internal const string TestBusinessType = "test.approval";

    /// <summary>声明字段清单的测试用业务类型，对应 <see cref="TestFieldedApprovalBusinessDescriptor"/>。</summary>
    internal const string FieldedBusinessType = "test.approval-fields";

    [Fact]
    public async Task Sequential_flow_should_advance_node_by_node_until_approved()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var firstApprover = Guid.NewGuid();
        var secondApprover = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "一级审批", 1, [firstApprover]),
            BuildApprovalNode("n2", "二级审批", 2, [secondApprover]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(
            BuildInput(businessType, code, "顺序审批"),
            initiator);

        Assert.Equal(ApprovalInstanceStatus.Running, instance.Status);
        Assert.Equal("n1", instance.CurrentNodeKey);
        Assert.Equal("一级审批", instance.CurrentNodeName);
        Assert.False(string.IsNullOrWhiteSpace(instance.Number));

        var firstTask = await GetSinglePendingTaskAsync(scope, instance.Id, firstApprover);
        await engine.ApproveAsync(firstTask.Id, firstApprover, "同意");

        var afterFirst = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Running, afterFirst.Status);
        Assert.Equal("n2", afterFirst.CurrentNodeKey);

        var secondTask = await GetSinglePendingTaskAsync(scope, instance.Id, secondApprover);
        await engine.ApproveAsync(secondTask.Id, secondApprover, "同意");

        var completed = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Approved, completed.Status);
        Assert.Null(completed.RunningKey);
        Assert.NotNull(completed.CompletedTime);

        var records = await scope.ServiceProvider.GetRequiredService<IApprovalRecordRepository>()
            .GetListAsync(item => item.InstanceId == instance.Id);
        Assert.Equal(3, records.Count);
        Assert.Contains(records, item => item.Action == ApprovalAction.Submit);
        Assert.Equal(2, records.Count(item => item.Action == ApprovalAction.Approve));
    }

    [Fact]
    public async Task Conditional_node_should_be_skipped_when_context_does_not_match()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var firstApprover = Guid.NewGuid();
        var financeApprover = Guid.NewGuid();
        var businessType = TestBusinessType;
        var conditionalNode = BuildApprovalNode("finance", "财务审批", 2, [financeApprover]);
        conditionalNode.ConditionJson =
            """{ "match": "all", "rules": [ { "field": "amount", "operator": "greaterOrEqual", "value": "1000" } ] }""";

        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "主管审批", 1, [firstApprover]),
            conditionalNode);

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(
            BuildInput(businessType, code, "小额报销", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "100"
            }),
            initiator);

        var task = await GetSinglePendingTaskAsync(scope, instance.Id, firstApprover);
        await engine.ApproveAsync(task.Id, firstApprover, "同意");

        var completed = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Approved, completed.Status);

        var financeTasks = await scope.ServiceProvider.GetRequiredService<IApprovalTaskRepository>()
            .GetListAsync(item => item.InstanceId == instance.Id && item.NodeKey == "finance");
        Assert.Empty(financeTasks);
    }

    [Fact]
    public async Task Conditional_middle_node_should_be_bypassed_without_skipping_later_approval()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var firstApprover = Guid.NewGuid();
        var skippedApprover = Guid.NewGuid();
        var finalApprover = Guid.NewGuid();
        var conditional = BuildApprovalNode("conditional", "条件审批", 2, [skippedApprover]);
        conditional.ConditionJson = LargeAmountCondition;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            TestBusinessType,
            BuildApprovalNode("first", "一级审批", 1, [firstApprover]),
            conditional,
            BuildApprovalNode("final", "最终审批", 3, [finalApprover]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(
            BuildInput(TestBusinessType, code, "条件节点免审", new Dictionary<string, string>
            {
                ["amount"] = "100"
            }),
            Guid.NewGuid());

        await engine.ApproveAsync(
            (await GetSinglePendingTaskAsync(scope, instance.Id, firstApprover)).Id,
            firstApprover,
            "同意");

        Assert.Empty(await scope.ServiceProvider.GetRequiredService<IApprovalTaskRepository>()
            .GetListAsync(item => item.InstanceId == instance.Id && item.AssigneeUserId == skippedApprover));
        Assert.Equal(
            finalApprover,
            (await GetSinglePendingTaskAsync(scope, instance.Id, finalApprover)).AssigneeUserId);
    }

    [Fact]
    public async Task Legacy_instance_without_node_rows_should_keep_pending_task_actionable()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var approver = Guid.NewGuid();
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            TestBusinessType,
            BuildApprovalNode("approval", "审批", 1, [approver]));
        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(TestBusinessType, code, "旧实例兼容"), Guid.NewGuid());
        var task = await GetSinglePendingTaskAsync(scope, instance.Id, approver);

        await scope.ServiceProvider.GetRequiredService<IApprovalInstanceNodeRepository>()
            .DeleteAsync(item => item.InstanceId == instance.Id);

        await engine.ApproveAsync(task.Id, approver, "同意");
        Assert.Equal(ApprovalInstanceStatus.Approved, (await ReloadInstanceAsync(scope, instance.Id)).Status);
    }

    [Fact]
    public async Task Transfer_after_before_sign_should_restore_original_task_and_advance_version()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var originalApprover = Guid.NewGuid();
        var signUser = await InsertActiveUserAsync(scope.ServiceProvider, "sign");
        var transferUser = await InsertActiveUserAsync(scope.ServiceProvider, "transfer");
        var node = BuildApprovalNode("approval", "审批", 1, [originalApprover]);
        node.AllowAddSign = true;
        node.AllowTransfer = true;
        var (_, code) = await SeedDefinitionAsync(scope.ServiceProvider, TestBusinessType, node);
        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(TestBusinessType, code, "加签转交"), Guid.NewGuid());
        var initialVersion = instance.Version;

        await engine.AddSignAsync(
            (await GetSinglePendingTaskAsync(scope, instance.Id, originalApprover)).Id,
            originalApprover,
            signUser,
            ApprovalAddSignPosition.Before,
            null);
        await engine.TransferAsync(
            (await GetSinglePendingTaskAsync(scope, instance.Id, signUser)).Id,
            signUser,
            transferUser,
            null);
        await engine.ApproveAsync(
            (await GetSinglePendingTaskAsync(scope, instance.Id, transferUser)).Id,
            transferUser,
            "同意");

        Assert.Equal(
            originalApprover,
            (await GetSinglePendingTaskAsync(scope, instance.Id, originalApprover)).AssigneeUserId);
        Assert.True((await ReloadInstanceAsync(scope, instance.Id)).Version >= initialVersion + 3);
    }

    [Fact]
    public async Task Transfer_should_reject_inactive_target_without_claiming_instance()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var approver = Guid.NewGuid();
        var node = BuildApprovalNode("approval", "审批", 1, [approver]);
        node.AllowTransfer = true;
        var (_, code) = await SeedDefinitionAsync(scope.ServiceProvider, TestBusinessType, node);
        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(TestBusinessType, code, "无效目标用户"), Guid.NewGuid());
        var initialVersion = instance.Version;
        var task = await GetSinglePendingTaskAsync(scope, instance.Id, approver);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => engine.TransferAsync(task.Id, approver, Guid.NewGuid(), null));

        Assert.Equal(ApprovalErrorCodes.InvalidTargetUser, exception.Code);
        Assert.Equal(ApprovalTaskStatus.Pending, (await scope.ServiceProvider
            .GetRequiredService<IApprovalTaskRepository>()
            .GetAsync(task.Id))!.Status);
        Assert.Equal(initialVersion, (await ReloadInstanceAsync(scope, instance.Id)).Version);
    }

    [Fact]
    public async Task All_mode_partial_approval_should_advance_instance_version()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var firstApprover = Guid.NewGuid();
        var secondApprover = Guid.NewGuid();
        var node = BuildApprovalNode("approval", "会签审批", 1, [firstApprover, secondApprover]);
        node.MultiApproveMode = ApprovalMultiApproveMode.All;
        var (_, code) = await SeedDefinitionAsync(scope.ServiceProvider, TestBusinessType, node);
        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(TestBusinessType, code, "会签并发版本"), Guid.NewGuid());
        var initialVersion = instance.Version;

        await engine.ApproveAsync(
            (await GetSinglePendingTaskAsync(scope, instance.Id, firstApprover)).Id,
            firstApprover,
            "同意");

        var afterPartialApproval = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Running, afterPartialApproval.Status);
        Assert.Equal(initialVersion + 1, afterPartialApproval.Version);
        Assert.Equal(
            secondApprover,
            (await GetSinglePendingTaskAsync(scope, instance.Id, secondApprover)).AssigneeUserId);
    }

    [Fact]
    public async Task Any_mode_should_complete_node_and_skip_remaining_tasks()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approvers = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var businessType = TestBusinessType;
        var node = BuildApprovalNode("n1", "或签审批", 1, approvers);
        node.MultiApproveMode = ApprovalMultiApproveMode.Any;

        var (_, code) = await SeedDefinitionAsync(scope.ServiceProvider, businessType, node);

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "或签"), initiator);

        var taskRepository = scope.ServiceProvider.GetRequiredService<IApprovalTaskRepository>();
        var pendingTasks = await taskRepository.GetListAsync(
            item => item.InstanceId == instance.Id && item.Status == ApprovalTaskStatus.Pending);
        Assert.Equal(3, pendingTasks.Count);

        var winner = pendingTasks.First(item => item.AssigneeUserId == approvers[0]);
        await engine.ApproveAsync(winner.Id, approvers[0], "通过");

        var completed = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Approved, completed.Status);

        var allTasks = await taskRepository.GetListAsync(item => item.InstanceId == instance.Id);
        Assert.Single(allTasks, item => item.Status == ApprovalTaskStatus.Approved);
        Assert.Equal(2, allTasks.Count(item => item.Status == ApprovalTaskStatus.Skipped));
    }

    [Fact]
    public async Task Cc_node_should_create_cc_records_without_blocking_flow()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var ccUser = Guid.NewGuid();
        var businessType = TestBusinessType;
        var ccNode = new ApprovalFlowNode
        {
            Id = Guid.NewGuid(),
            NodeKey = "cc1",
            Name = "抄送财务",
            NodeType = ApprovalNodeType.Cc,
            SortOrder = 1,
            CcConfigJson = JsonSerializer.Serialize(new { userIds = new[] { ccUser }, initiator = false })
        };

        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            ccNode,
            BuildApprovalNode("n1", "审批", 2, [approver]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "抄送流程"), initiator);

        Assert.Equal(ApprovalInstanceStatus.Running, instance.Status);
        Assert.Equal("n1", instance.CurrentNodeKey);

        var ccRecords = await scope.ServiceProvider.GetRequiredService<IApprovalCcRepository>()
            .GetListAsync(item => item.InstanceId == instance.Id);
        var cc = Assert.Single(ccRecords);
        Assert.Equal(ccUser, cc.UserId);
        Assert.Null(cc.ReadAt);

        await engine.MarkCcReadAsync(cc.Id, ccUser);
        var reloadedCc = await scope.ServiceProvider.GetRequiredService<IApprovalCcRepository>().GetAsync(cc.Id);
        Assert.NotNull(reloadedCc!.ReadAt);
    }

    [Fact]
    public async Task Return_to_initiator_should_pause_instance_and_resubmit_should_restart_from_first_node()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "审批", 1, [approver]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "退回流程"), initiator);

        var task = await GetSinglePendingTaskAsync(scope, instance.Id, approver);
        await engine.ReturnToInitiatorAsync(task.Id, approver, "请补充附件");

        var returned = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Returned, returned.Status);
        Assert.NotNull(returned.RunningKey);

        var resubmitted = await engine.ResubmitAsync(
            instance.Id,
            "补充后的标题",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["amount"] = "2000" },
            initiator);

        Assert.Equal(ApprovalInstanceStatus.Running, resubmitted.Status);
        Assert.Equal("补充后的标题", resubmitted.Title);

        var pendingTasks = await scope.ServiceProvider.GetRequiredService<IApprovalTaskRepository>()
            .GetListAsync(item => item.InstanceId == instance.Id && item.Status == ApprovalTaskStatus.Pending);
        var pending = Assert.Single(pendingTasks);
        Assert.Equal(2, pending.Round);
        Assert.Equal("n1", pending.NodeKey);
    }

    [Fact]
    public async Task Withdraw_should_end_instance_and_release_business_object()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "审批", 1, [approver]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var input = BuildInput(businessType, code, "撤回流程");
        var instance = await engine.StartAsync(input, initiator);

        await engine.WithdrawAsync(instance.Id, initiator, "业务取消");

        var withdrawn = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Withdrawn, withdrawn.Status);
        Assert.Null(withdrawn.RunningKey);

        var pendingTasks = await scope.ServiceProvider.GetRequiredService<IApprovalTaskRepository>()
            .GetListAsync(item => item.InstanceId == instance.Id && item.Status == ApprovalTaskStatus.Pending);
        Assert.Empty(pendingTasks);

        // 撤回后同一业务对象可以重新发起
        var restarted = await engine.StartAsync(input, initiator);
        Assert.NotEqual(instance.Id, restarted.Id);
    }

    [Fact]
    public async Task Starting_second_instance_for_same_business_object_should_be_rejected()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "审批", 1, [Guid.NewGuid()]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var input = BuildInput(businessType, code, "重复发起");
        await engine.StartAsync(input, initiator);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => engine.StartAsync(input, initiator));
        Assert.Equal(ApprovalErrorCodes.InstanceAlreadyRunning, exception.Code);
    }

    [Fact]
    public void Timeout_scan_job_should_be_registered_as_builtin_schedule_task()
    {
        using var scope = fixture.Services.CreateScope();

        var providers = scope.ServiceProvider.GetServices<IScheduleTaskDefinitionProvider>();
        var definition = providers
            .SelectMany(provider => provider.GetDefinitions())
            .SingleOrDefault(item => item.Key == ApprovalConsts.TimeoutScanTaskKey);

        Assert.NotNull(definition);
        Assert.Equal(ApprovalConsts.TimeoutScanTaskGroup, definition!.Group);
        Assert.Equal(typeof(Sim.Approval.Application.Jobs.ApprovalTimeoutScanJob), definition.JobType);
        Assert.Equal(ApprovalConsts.TimeoutScanTaskCron, definition.DefaultCronExpression);
        Assert.True(definition.DefaultEnabled);
    }

    [Fact]
    public async Task Business_status_query_should_return_latest_snapshot()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var businessType = TestBusinessType;
        var businessId = $"biz-{Guid.NewGuid():N}";
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "审批", 1, [approver]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var input = BuildInput(businessType, code, "状态查询", businessId: businessId);
        var instance = await engine.StartAsync(input, initiator);

        var statusQuery = scope.ServiceProvider.GetRequiredService<IApprovalStatusQuery>();
        var snapshot = await statusQuery.FindAsync(businessType, businessId);
        Assert.NotNull(snapshot);
        Assert.Equal(instance.Id, snapshot!.InstanceId);
        Assert.True(snapshot.IsRunning);
        Assert.Equal("审批", snapshot.CurrentNodeName);

        var task = await GetSinglePendingTaskAsync(scope, instance.Id, approver);
        await engine.ApproveAsync(task.Id, approver, "同意");

        var finished = await statusQuery.FindAsync(businessType, businessId);
        Assert.NotNull(finished);
        Assert.False(finished!.IsRunning);
        Assert.Equal(nameof(ApprovalInstanceStatus.Approved), finished.Status);
        Assert.Null(finished.CurrentNodeName);
    }

    [Fact]
    public async Task Concurrent_approve_on_same_task_should_let_exactly_one_succeed()
    {
        var initiator = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var businessType = TestBusinessType;

        Guid instanceId;
        Guid taskId;
        using (var setupScope = fixture.Services.CreateScope())
        {
            var provider = setupScope.ServiceProvider;
            await EnsureNumberingDefinitionAsync(provider);
            var (_, code) = await SeedDefinitionAsync(
                provider,
                businessType,
                BuildApprovalNode("n1", "审批", 1, [approver]));

            var engine = provider.GetRequiredService<ApprovalEngine>();
            var instance = await engine.StartAsync(BuildInput(businessType, code, "并发审批"), initiator);
            instanceId = instance.Id;
            taskId = (await GetSinglePendingTaskAsync(setupScope, instance.Id, approver)).Id;
        }

        // 两个独立作用域并发处理同一任务：只有一个操作可以生效，另一个必须被拒绝。
        var results = await Task.WhenAll(
            ApproveInNewScopeAsync(taskId, approver),
            ApproveInNewScopeAsync(taskId, approver));

        Assert.Equal(1, results.Count(item => item is null));
        var failure = Assert.IsType<BusinessException>(results.Single(item => item is not null));
        // 落败方的错误取决于交错点：任务已被处理、实例已结束或实例版本已被推进。
        Assert.Contains(
            failure.Code,
            new[]
            {
                ApprovalErrorCodes.TaskAlreadyHandled,
                ApprovalErrorCodes.InstanceNotRunning,
                ApprovalErrorCodes.ConcurrentModified
            });

        using var assertScope = fixture.Services.CreateScope();
        var tasks = await assertScope.ServiceProvider.GetRequiredService<IApprovalTaskRepository>()
            .GetListAsync(item => item.InstanceId == instanceId);
        Assert.Single(tasks, item => item.Status == ApprovalTaskStatus.Approved);
        Assert.Equal(
            ApprovalInstanceStatus.Approved,
            (await ReloadInstanceAsync(assertScope, instanceId)).Status);

        // 流程只推进一次：审批流水只应有一条同意记录。
        var records = await assertScope.ServiceProvider.GetRequiredService<IApprovalRecordRepository>()
            .GetListAsync(item => item.InstanceId == instanceId);
        Assert.Equal(1, records.Count(item => item.Action == ApprovalAction.Approve));
    }

    [Fact]
    public async Task Concurrent_submit_for_same_business_object_should_let_exactly_one_succeed()
    {
        var initiator = Guid.NewGuid();
        var businessType = TestBusinessType;

        string code;
        using (var setupScope = fixture.Services.CreateScope())
        {
            var provider = setupScope.ServiceProvider;
            await EnsureNumberingDefinitionAsync(provider);
            (_, code) = await SeedDefinitionAsync(
                provider,
                businessType,
                BuildApprovalNode("n1", "审批", 1, [Guid.NewGuid()]));
        }

        var input = BuildInput(businessType, code, "并发发起");
        var results = await Task.WhenAll(
            SubmitInNewScopeAsync(input, initiator),
            SubmitInNewScopeAsync(input, initiator));

        Assert.Equal(1, results.Count(item => item is null));
        var failure = Assert.IsType<BusinessException>(results.Single(item => item is not null));
        Assert.Equal(ApprovalErrorCodes.InstanceAlreadyRunning, failure.Code);
    }

    [Fact]
    public async Task Urge_should_be_rejected_within_throttle_interval()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "审批", 1, [approver]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "催办"), initiator);

        await engine.UrgeAsync(instance.Id, initiator);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => engine.UrgeAsync(instance.Id, initiator));
        Assert.Equal(ApprovalErrorCodes.UrgeTooFrequent, exception.Code);
    }

    [Fact]
    public async Task Withdraw_should_be_rejected_after_withdraw_window_expired()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedDefinitionWithWindowAsync(
            scope.ServiceProvider,
            businessType,
            withdrawWindowHours: 1,
            BuildApprovalNode("n1", "审批", 1, [Guid.NewGuid()]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "超窗撤回"), initiator);

        // 回写发起时间，模拟超过撤回时间窗
        var instanceRepository = scope.ServiceProvider.GetRequiredService<IApprovalInstanceRepository>();
        var stored = await instanceRepository.GetAsync(instance.Id);
        stored!.CreationTime = DateTime.Now.AddHours(-2);
        await instanceRepository.UpdateAsync(stored);

        var exception = await Assert.ThrowsAsync<BusinessException>(
            () => engine.WithdrawAsync(instance.Id, initiator, "业务取消"));
        Assert.Equal(ApprovalErrorCodes.WithdrawWindowExpired, exception.Code);

        Assert.Equal(ApprovalInstanceStatus.Running, (await ReloadInstanceAsync(scope, instance.Id)).Status);
    }

    [Fact]
    public async Task Returned_instance_should_be_withdrawable_by_initiator()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "审批", 1, [approver]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "退回后撤回"), initiator);

        var task = await GetSinglePendingTaskAsync(scope, instance.Id, approver);
        await engine.ReturnToInitiatorAsync(task.Id, approver, "请补充材料");

        Assert.Equal(ApprovalInstanceStatus.Returned, (await ReloadInstanceAsync(scope, instance.Id)).Status);

        await engine.WithdrawAsync(instance.Id, initiator, "不再需要");

        var withdrawn = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Withdrawn, withdrawn.Status);
        Assert.Null(withdrawn.RunningKey);
    }

    // ── 有向图推进（分支 / 合流 / 剪枝）────────────────────────────────────

    [Fact]
    public async Task Exclusive_gateway_should_only_activate_matching_branch()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var bigApprover = Guid.NewGuid();
        var smallApprover = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedGraphDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            [
                BuildNode("start", "开始", ApprovalNodeType.Start, 0),
                BuildNode("gw", "金额判定", ApprovalNodeType.ExclusiveGateway, 1),
                BuildApprovalNode("big", "大额审批", 2, [bigApprover]),
                BuildApprovalNode("small", "小额审批", 2, [smallApprover]),
                BuildNode("end", "结束", ApprovalNodeType.End, 3)
            ],
            [
                BuildEdge("start", "gw"),
                BuildEdge("gw", "big", conditionJson: LargeAmountCondition),
                BuildEdge("gw", "small", sortOrder: 2),
                BuildEdge("big", "end"),
                BuildEdge("small", "end")
            ]);

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(
            BuildInput(businessType, code, "大额报销", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "2000"
            }),
            initiator);

        Assert.Equal(ApprovalInstanceStatus.Running, instance.Status);
        Assert.Equal("big", instance.CurrentNodeKey);

        var states = await GetNodeStatesAsync(scope, instance.Id);
        Assert.Equal(ApprovalNodeState.Completed, states["start"]);
        Assert.Equal(ApprovalNodeState.Completed, states["gw"]);
        Assert.Equal(ApprovalNodeState.Active, states["big"]);
        // 未选中的分支立即剪枝，不会生成任务也不会等它。
        Assert.Equal(ApprovalNodeState.Skipped, states["small"]);
        Assert.Equal(ApprovalNodeState.Pending, states["end"]);

        var smallTasks = await scope.ServiceProvider.GetRequiredService<IApprovalTaskRepository>()
            .GetListAsync(item => item.InstanceId == instance.Id && item.NodeKey == "small");
        Assert.Empty(smallTasks);

        var bigTask = await GetSinglePendingTaskAsync(scope, instance.Id, bigApprover);
        await engine.ApproveAsync(bigTask.Id, bigApprover, "同意");

        var completed = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Approved, completed.Status);

        var finalStates = await GetNodeStatesAsync(scope, instance.Id);
        Assert.Equal(ApprovalNodeState.Completed, finalStates["end"]);
        Assert.Equal(ApprovalNodeState.Skipped, finalStates["small"]);
    }

    [Fact]
    public async Task Parallel_fork_should_wait_for_every_branch_before_joining()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approverA = Guid.NewGuid();
        var approverB = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedGraphDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            [
                BuildNode("start", "开始", ApprovalNodeType.Start, 0),
                BuildNode("fork", "并行开始", ApprovalNodeType.ParallelGateway, 1),
                BuildApprovalNode("a", "分支甲", 2, [approverA]),
                BuildApprovalNode("b", "分支乙", 2, [approverB]),
                BuildNode("join", "并行汇合", ApprovalNodeType.ParallelGateway, 3),
                BuildNode("end", "结束", ApprovalNodeType.End, 4)
            ],
            [
                BuildEdge("start", "fork"),
                BuildEdge("fork", "a"),
                BuildEdge("fork", "b"),
                BuildEdge("a", "join"),
                BuildEdge("b", "join"),
                BuildEdge("join", "end")
            ]);

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "并行审批"), initiator);

        Assert.Equal(ApprovalInstanceStatus.Running, instance.Status);
        Assert.Equal("a", instance.CurrentNodeKey);

        var states = await GetNodeStatesAsync(scope, instance.Id);
        Assert.Equal(ApprovalNodeState.Active, states["a"]);
        Assert.Equal(ApprovalNodeState.Active, states["b"]);
        Assert.Equal(ApprovalNodeState.Pending, states["join"]);
        Assert.Equal(ApprovalNodeState.Pending, states["end"]);

        // 先处理「非当前节点」的分支：并行分支各自独立待办，处理顺序不受 CurrentNodeKey 限制。
        var taskB = await GetSinglePendingTaskAsync(scope, instance.Id, approverB);
        await engine.ApproveAsync(taskB.Id, approverB, "同意");

        var afterB = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Running, afterB.Status);
        Assert.Equal("a", afterB.CurrentNodeKey);

        var afterBStates = await GetNodeStatesAsync(scope, instance.Id);
        Assert.Equal(ApprovalNodeState.Completed, afterBStates["b"]);
        Assert.Equal(ApprovalNodeState.Active, afterBStates["a"]);
        // 合流节点必须等待另一条分支到达。
        Assert.Equal(ApprovalNodeState.Pending, afterBStates["join"]);
        Assert.Equal(ApprovalNodeState.Pending, afterBStates["end"]);

        var taskA = await GetSinglePendingTaskAsync(scope, instance.Id, approverA);
        await engine.ApproveAsync(taskA.Id, approverA, "同意");

        var completed = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Approved, completed.Status);

        var finalStates = await GetNodeStatesAsync(scope, instance.Id);
        Assert.Equal(ApprovalNodeState.Completed, finalStates["a"]);
        Assert.Equal(ApprovalNodeState.Completed, finalStates["join"]);
        Assert.Equal(ApprovalNodeState.Completed, finalStates["end"]);
    }

    [Fact]
    public async Task Join_should_not_wait_for_branch_pruned_by_exclusive_gateway()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approverA = Guid.NewGuid();
        var approverX = Guid.NewGuid();
        var approverY = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedGraphDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            [
                BuildNode("start", "开始", ApprovalNodeType.Start, 0),
                BuildNode("fork", "并行开始", ApprovalNodeType.ParallelGateway, 1),
                BuildApprovalNode("a", "分支甲", 2, [approverA]),
                BuildNode("gw", "金额判定", ApprovalNodeType.ExclusiveGateway, 2),
                BuildApprovalNode("x", "大额审批", 3, [approverX]),
                BuildApprovalNode("y", "小额审批", 3, [approverY]),
                BuildNode("join", "并行汇合", ApprovalNodeType.ParallelGateway, 4),
                BuildNode("end", "结束", ApprovalNodeType.End, 5)
            ],
            [
                BuildEdge("start", "fork"),
                BuildEdge("fork", "a"),
                BuildEdge("fork", "gw"),
                BuildEdge("a", "join"),
                BuildEdge("gw", "x", conditionJson: LargeAmountCondition),
                BuildEdge("gw", "y", sortOrder: 2),
                BuildEdge("x", "join"),
                BuildEdge("y", "join"),
                BuildEdge("join", "end")
            ]);

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(
            BuildInput(businessType, code, "小额报销并行", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["amount"] = "100"
            }),
            initiator);

        var states = await GetNodeStatesAsync(scope, instance.Id);
        Assert.Equal(ApprovalNodeState.Completed, states["gw"]);
        Assert.Equal(ApprovalNodeState.Skipped, states["x"]);
        Assert.Equal(ApprovalNodeState.Active, states["a"]);
        Assert.Equal(ApprovalNodeState.Active, states["y"]);
        Assert.Equal(ApprovalNodeState.Pending, states["join"]);

        var taskA = await GetSinglePendingTaskAsync(scope, instance.Id, approverA);
        await engine.ApproveAsync(taskA.Id, approverA, "同意");

        // 被剪枝的分支不再计入等待，但另一条并行分支仍须到达。
        var afterA = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Running, afterA.Status);
        var afterAStates = await GetNodeStatesAsync(scope, instance.Id);
        Assert.Equal(ApprovalNodeState.Completed, afterAStates["a"]);
        Assert.Equal(ApprovalNodeState.Pending, afterAStates["join"]);

        var taskY = await GetSinglePendingTaskAsync(scope, instance.Id, approverY);
        await engine.ApproveAsync(taskY.Id, approverY, "同意");

        var completed = await ReloadInstanceAsync(scope, instance.Id);
        Assert.Equal(ApprovalInstanceStatus.Approved, completed.Status);

        var finalStates = await GetNodeStatesAsync(scope, instance.Id);
        Assert.Equal(ApprovalNodeState.Completed, finalStates["join"]);
        Assert.Equal(ApprovalNodeState.Completed, finalStates["end"]);
    }

    [Fact]
    public async Task Linear_definition_should_persist_synthesized_boundaries_and_flow_records()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var firstApprover = Guid.NewGuid();
        var secondApprover = Guid.NewGuid();
        var businessType = TestBusinessType;
        // 只有节点、没有连线：读取时合成 Start → n1 → n2 → End（第一阶段等价性）。
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "一级审批", 1, [firstApprover]),
            BuildApprovalNode("n2", "二级审批", 2, [secondApprover]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "线性等价"), initiator);

        var states = await GetNodeStatesAsync(scope, instance.Id);
        Assert.Equal(4, states.Count);
        Assert.Equal(ApprovalNodeState.Completed, states[ApprovalConsts.SynthesizedStartNodeKey]);
        Assert.Equal(ApprovalNodeState.Active, states["n1"]);
        Assert.Equal(ApprovalNodeState.Pending, states["n2"]);
        Assert.Equal(ApprovalNodeState.Pending, states[ApprovalConsts.SynthesizedEndNodeKey]);

        var recordRepository = scope.ServiceProvider.GetRequiredService<IApprovalRecordRepository>();
        var records = await recordRepository.GetListAsync(item => item.InstanceId == instance.Id);
        // 发起阶段只有一条提交流水，且已指向第一个真实节点。
        var submit = Assert.Single(records);
        Assert.Equal(ApprovalAction.Submit, submit.Action);
        Assert.Null(submit.FromNodeKey);
        Assert.Equal("n1", submit.ToNodeKey);

        var firstTask = await GetSinglePendingTaskAsync(scope, instance.Id, firstApprover);
        await engine.ApproveAsync(firstTask.Id, firstApprover, "同意");

        records = await recordRepository.GetListAsync(item => item.InstanceId == instance.Id);
        var firstApprove = Assert.Single(
            records,
            item => item.Action == ApprovalAction.Approve && item.NodeKey == "n1");
        Assert.Equal("n1", firstApprove.FromNodeKey);
        Assert.Equal("n2", firstApprove.ToNodeKey);

        var secondTask = await GetSinglePendingTaskAsync(scope, instance.Id, secondApprover);
        await engine.ApproveAsync(secondTask.Id, secondApprover, "同意");

        Assert.Equal(ApprovalInstanceStatus.Approved, (await ReloadInstanceAsync(scope, instance.Id)).Status);
        Assert.Equal(
            ApprovalNodeState.Completed,
            (await GetNodeStatesAsync(scope, instance.Id))[ApprovalConsts.SynthesizedEndNodeKey]);

        records = await recordRepository.GetListAsync(item => item.InstanceId == instance.Id);
        Assert.Equal(3, records.Count);
        var secondApprove = Assert.Single(
            records,
            item => item.Action == ApprovalAction.Approve && item.NodeKey == "n2");
        Assert.Equal("n2", secondApprove.FromNodeKey);
        Assert.Null(secondApprove.ToNodeKey);
    }

    // ── 字段元数据与附件（P2）──────────────────────────────────────────────

    [Fact]
    public async Task Field_values_should_be_snapshotted_with_declared_labels_and_types()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var bigApprover = Guid.NewGuid();
        var (_, code) = await SeedGraphDefinitionAsync(
            scope.ServiceProvider,
            FieldedBusinessType,
            [
                BuildNode("start", "开始", ApprovalNodeType.Start, 0),
                BuildNode("gw", "金额判定", ApprovalNodeType.ExclusiveGateway, 1),
                BuildApprovalNode("big", "大额审批", 2, [bigApprover]),
                BuildApprovalNode("small", "小额审批", 2, [Guid.NewGuid()]),
                BuildNode("end", "结束", ApprovalNodeType.End, 3)
            ],
            [
                BuildEdge("start", "gw"),
                BuildEdge("gw", "big", conditionJson: LargeAmountCondition),
                BuildEdge("gw", "small", sortOrder: 2),
                BuildEdge("big", "end"),
                BuildEdge("small", "end")
            ]);

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var input = BuildInput(FieldedBusinessType, code, "差旅报销");
        input.Fields =
        [
            new ApprovalFieldValueDto { Key = "amount", Value = "2000" },
            new ApprovalFieldValueDto { Key = "category", Value = "travel" }
        ];
        // 字段值与扁平上下文可并用：未在 Fields 中出现的键仍参与条件求值与展示。
        input.Context["reason"] = "客户拜访";

        var instance = await engine.StartAsync(input, initiator);

        // 字段值写进快照后仍可用于条件求值。
        Assert.Equal("big", instance.CurrentNodeKey);

        var detail = await scope.ServiceProvider.GetRequiredService<ApprovalQueryService>()
            .GetDetailAsync(instance.Id, initiator, canMonitor: false);

        var amount = Assert.Single(detail.Context.Fields, item => item.Key == "amount");
        Assert.Equal("金额", amount.DisplayName);
        Assert.Equal(nameof(ApprovalFieldType.Decimal), amount.Type);
        Assert.Equal("2000", amount.Value);

        var category = Assert.Single(detail.Context.Fields, item => item.Key == "category");
        Assert.Equal(nameof(ApprovalFieldType.Select), category.Type);
        Assert.Equal(2, category.Options.Count);
        Assert.Equal("差旅", Assert.Single(category.Options, item => item.Value == "travel").Label);

        // 未声明字段按「标签即标识、文本类型」兜底。
        var reason = Assert.Single(detail.Context.Fields, item => item.Key == "reason");
        Assert.Equal("reason", reason.DisplayName);
        Assert.Equal(nameof(ApprovalFieldType.String), reason.Type);
        Assert.Equal("客户拜访", reason.Value);
    }

    [Fact]
    public async Task Start_should_reject_missing_required_field()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var businessType = FieldedBusinessType;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "审批", 1, [Guid.NewGuid()]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            engine.StartAsync(BuildInput(businessType, code, "缺少必填字段"), Guid.NewGuid()));

        Assert.Equal(ApprovalErrorCodes.InvalidRequest, exception.Code);
        Assert.Contains("金额", exception.Message);
    }

    [Fact]
    public void Attachment_field_should_not_be_condition_comparable()
    {
        Assert.False(ApprovalFieldSchema.ParseFieldType(nameof(ApprovalFieldType.Attachment))
            .IsConditionComparable());
        Assert.True(ApprovalFieldSchema.ParseFieldType(nameof(ApprovalFieldType.Decimal))
            .IsConditionComparable());
        // 未声明的类型按文本处理。
        Assert.Equal(ApprovalFieldType.String, ApprovalFieldSchema.ParseFieldType("unknown"));
    }

    [Fact]
    public void Legacy_flat_context_should_still_be_readable()
    {
        var values = ApprovalRuntimeJson.DeserializeContextValues("""{ "amount": "100", "flag": true }""");
        Assert.Equal("100", values["amount"]);
        Assert.Equal("true", values["flag"]);

        var snapshot = ApprovalRuntimeJson.DeserializeContextSnapshot("""{ "amount": "100" }""");
        var field = Assert.Single(snapshot.Fields);
        Assert.Equal("amount", field.Key);
        Assert.Equal("amount", field.DisplayName);
        Assert.Equal(nameof(ApprovalFieldType.String), field.Type);

        // 空 JSON 与非法 JSON 都退化为空快照，不抛异常。
        Assert.Empty(ApprovalRuntimeJson.DeserializeContextSnapshot(null).Fields);
        Assert.Empty(ApprovalRuntimeJson.DeserializeContextValues("not-json").Keys);
    }

    [Fact]
    public async Task Instance_and_task_attachments_should_be_stored_listed_and_downloadable()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "审批", 1, [approver]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "带附件的审批"), initiator);
        var task = await GetSinglePendingTaskAsync(scope, instance.Id, approver);

        var attachmentService = scope.ServiceProvider.GetRequiredService<ApprovalAttachmentService>();
        var png = OnePixelPng();

        var instanceAttachment = await attachmentService.UploadForInstanceAsync(
            instance.Id,
            initiator,
            BuildFile(png, "报销单.png"));
        Assert.Equal("报销单.png", instanceAttachment.FileName);
        Assert.Equal(png.LongLength, instanceAttachment.Size);
        Assert.Null(instanceAttachment.TaskId);
        Assert.Equal(initiator, instanceAttachment.UploaderUserId);

        var taskAttachment = await attachmentService.UploadForTaskAsync(
            task.Id,
            approver,
            BuildFile(png, "凭证.png"));
        Assert.Equal(task.Id, taskAttachment.TaskId);
        Assert.Equal(approver, taskAttachment.UploaderUserId);

        var persisted = await scope.ServiceProvider.GetRequiredService<IApprovalAttachmentRepository>()
            .GetListAsync(item => item.InstanceId == instance.Id);
        Assert.Equal(2, persisted.Count);
        Assert.All(persisted, item => Assert.NotEmpty(item.FileId));

        var detail = await scope.ServiceProvider.GetRequiredService<ApprovalQueryService>()
            .GetDetailAsync(instance.Id, initiator, canMonitor: false);
        Assert.Equal(2, detail.Attachments.Count);
        Assert.Equal(instanceAttachment.Id, detail.Attachments[0].Id);
        Assert.Equal(taskAttachment.Id, detail.Attachments[1].Id);
        Assert.True(detail.CanUploadAttachment);

        // 处理人可下载实例级与任务级附件，内容与上传一致。
        foreach (var expected in new[]
                 {
                     (Id: instanceAttachment.Id, FileName: "报销单.png"),
                     (Id: taskAttachment.Id, FileName: "凭证.png")
                 })
        {
            using var content = await attachmentService.DownloadAsync(expected.Id, approver);
            Assert.Equal(expected.FileName, content.FileName);
            await using var stream = content.GetStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Assert.Equal(png, buffer.ToArray());
        }

        // 与实例无关的用户看不到附件（与不存在同样报错，避免探测）。
        var denied = await Assert.ThrowsAsync<BusinessException>(() =>
            attachmentService.DownloadAsync(instanceAttachment.Id, outsider));
        Assert.Equal(ApprovalErrorCodes.AttachmentNotFound, denied.Code);
    }

    [Fact]
    public async Task Attachment_upload_should_enforce_uploader_and_instance_state()
    {
        using var scope = fixture.Services.CreateScope();
        await EnsureNumberingDefinitionAsync(scope.ServiceProvider);

        var initiator = Guid.NewGuid();
        var approver = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var businessType = TestBusinessType;
        var (_, code) = await SeedDefinitionAsync(
            scope.ServiceProvider,
            businessType,
            BuildApprovalNode("n1", "审批", 1, [approver]));

        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        var instance = await engine.StartAsync(BuildInput(businessType, code, "附件权限"), initiator);
        var task = await GetSinglePendingTaskAsync(scope, instance.Id, approver);
        var attachmentService = scope.ServiceProvider.GetRequiredService<ApprovalAttachmentService>();
        var png = OnePixelPng();

        var notInitiator = await Assert.ThrowsAsync<BusinessException>(() =>
            attachmentService.UploadForInstanceAsync(instance.Id, approver, BuildFile(png, "越权.png")));
        Assert.Equal(ApprovalErrorCodes.NotInitiator, notInitiator.Code);

        var notAssignee = await Assert.ThrowsAsync<BusinessException>(() =>
            attachmentService.UploadForTaskAsync(task.Id, outsider, BuildFile(png, "越权.png")));
        Assert.Equal(ApprovalErrorCodes.NotTaskAssignee, notAssignee.Code);

        await engine.ApproveAsync(task.Id, approver, "同意");
        Assert.Equal(
            ApprovalInstanceStatus.Approved,
            (await ReloadInstanceAsync(scope, instance.Id)).Status);

        var finished = await Assert.ThrowsAsync<BusinessException>(() =>
            attachmentService.UploadForInstanceAsync(instance.Id, initiator, BuildFile(png, "事后.png")));
        Assert.Equal(ApprovalErrorCodes.InstanceNotRunning, finished.Code);

        var detail = await scope.ServiceProvider.GetRequiredService<ApprovalQueryService>()
            .GetDetailAsync(instance.Id, initiator, canMonitor: false);
        Assert.False(detail.CanUploadAttachment);
        Assert.Empty(detail.Attachments);
    }

    // ── 测试辅助 ──────────────────────────────────────────────────────────

    private const string LargeAmountCondition =
        """{ "match": "all", "rules": [ { "field": "amount", "operator": "greaterOrEqual", "value": "1000" } ] }""";

    private async Task<BusinessException?> ApproveInNewScopeAsync(Guid taskId, Guid approver)
    {
        using var scope = fixture.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        try
        {
            await engine.ApproveAsync(taskId, approver, "并发同意");
            return null;
        }
        catch (BusinessException exception)
        {
            return exception;
        }
    }

    private async Task<BusinessException?> SubmitInNewScopeAsync(ApprovalSubmitInput input, Guid initiator)
    {
        using var scope = fixture.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<ApprovalEngine>();
        try
        {
            await engine.StartAsync(input, initiator);
            return null;
        }
        catch (BusinessException exception)
        {
            return exception;
        }
    }

    private static ApprovalSubmitInput BuildInput(
        string businessType,
        string definitionCode,
        string title,
        IReadOnlyDictionary<string, string>? context = null,
        string? businessId = null) => new()
    {
        DefinitionCode = definitionCode,
        BusinessType = businessType,
        BusinessId = businessId ?? $"biz-{Guid.NewGuid():N}",
        Title = title,
        Context = context is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(context, StringComparer.Ordinal)
    };

    private static ApprovalFlowNode BuildApprovalNode(
        string nodeKey,
        string name,
        int sortOrder,
        IReadOnlyCollection<Guid> approvers)
    {
        Assert.NotEmpty(approvers);
        return new ApprovalFlowNode
        {
            Id = Guid.NewGuid(),
            NodeKey = nodeKey,
            Name = name,
            NodeType = ApprovalNodeType.Approval,
            SortOrder = sortOrder,
            ApproverType = ApprovalApproverType.SpecifiedUsers,
            ApproverUserIds = ApprovalRuntimeJson.SerializeUserIds(approvers),
            MultiApproveMode = ApprovalMultiApproveMode.All
        };
    }

    private static Task<(Guid DefinitionId, string Code)> SeedDefinitionAsync(
        IServiceProvider services,
        string businessType,
        params ApprovalFlowNode[] nodes) =>
        SeedDefinitionWithWindowAsync(services, businessType, withdrawWindowHours: null, nodes);

    /// <summary>发起带撤回时间窗的流程定义（<paramref name="withdrawWindowHours"/> 为 null 表示不限窗）。</summary>
    private static async Task<(Guid DefinitionId, string Code)> SeedDefinitionWithWindowAsync(
        IServiceProvider services,
        string businessType,
        int? withdrawWindowHours,
        params ApprovalFlowNode[] nodes)
    {
        var definitionRepository = services.GetRequiredService<IApprovalFlowDefinitionRepository>();
        var nodeRepository = services.GetRequiredService<IApprovalFlowNodeRepository>();

        var definition = new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(),
            Code = $"test-{Guid.NewGuid():N}",
            Name = "测试流程",
            BusinessType = businessType,
            Version = 1,
            Status = ApprovalFlowStatus.Published,
            AllowWithdraw = true,
            WithdrawWindowHours = withdrawWindowHours,
            PublishedTime = DateTime.Now
        };
        await definitionRepository.InsertAsync(definition);

        foreach (var node in nodes)
        {
            node.DefinitionId = definition.Id;
        }

        await nodeRepository.InsertManyAsync(nodes.ToList());
        return (definition.Id, definition.Code);
    }

    /// <summary>发起带连线的（有向图）流程定义，用于分支 / 合流场景。</summary>
    private static async Task<(Guid DefinitionId, string Code)> SeedGraphDefinitionAsync(
        IServiceProvider services,
        string businessType,
        IReadOnlyList<ApprovalFlowNode> nodes,
        IReadOnlyList<ApprovalFlowEdge> edges)
    {
        var definitionRepository = services.GetRequiredService<IApprovalFlowDefinitionRepository>();
        var nodeRepository = services.GetRequiredService<IApprovalFlowNodeRepository>();
        var edgeRepository = services.GetRequiredService<IApprovalFlowEdgeRepository>();

        var definition = new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(),
            Code = $"test-{Guid.NewGuid():N}",
            Name = "测试图形流程",
            BusinessType = businessType,
            Version = 1,
            Status = ApprovalFlowStatus.Published,
            AllowWithdraw = true,
            PublishedTime = DateTime.Now
        };
        await definitionRepository.InsertAsync(definition);

        foreach (var node in nodes)
        {
            node.DefinitionId = definition.Id;
        }

        await nodeRepository.InsertManyAsync(nodes.ToList());

        foreach (var edge in edges)
        {
            edge.DefinitionId = definition.Id;
        }

        await edgeRepository.InsertManyAsync(edges.ToList());
        return (definition.Id, definition.Code);
    }

    private static ApprovalFlowNode BuildNode(
        string nodeKey,
        string name,
        ApprovalNodeType nodeType,
        int sortOrder) => new()
    {
        Id = Guid.NewGuid(),
        NodeKey = nodeKey,
        Name = name,
        NodeType = nodeType,
        SortOrder = sortOrder
    };

    private static ApprovalFlowEdge BuildEdge(
        string fromNodeKey,
        string toNodeKey,
        int sortOrder = 1,
        string? conditionJson = null) => new()
    {
        Id = Guid.NewGuid(),
        FromNodeKey = fromNodeKey,
        ToNodeKey = toNodeKey,
        SortOrder = sortOrder,
        ConditionJson = conditionJson
    };

    private static async Task<Dictionary<string, ApprovalNodeState>> GetNodeStatesAsync(
        IServiceScope scope,
        Guid instanceId)
    {
        var rows = await scope.ServiceProvider.GetRequiredService<IApprovalInstanceNodeRepository>()
            .GetListAsync(item => item.InstanceId == instanceId);
        return rows.ToDictionary(item => item.NodeKey, item => item.State, StringComparer.Ordinal);
    }

    private static async Task EnsureNumberingDefinitionAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<ISqlSugarRepository<BusinessNumberDefinitionEntity>>();
        var exists = await repository.GetAsync(item =>
            item.TenantScope == "host" &&
            item.BusinessKey == ApprovalConsts.InstanceNumberBusinessKey);
        if (exists is not null)
        {
            return;
        }

        await repository.InsertAsync(new BusinessNumberDefinitionEntity(Guid.NewGuid())
        {
            TenantScope = "host",
            BusinessKey = ApprovalConsts.InstanceNumberBusinessKey,
            DisplayName = "审批实例编号",
            Format = ApprovalConsts.InstanceNumberFormat,
            InitialSequence = 1,
            Enabled = true
        });
    }

    private static async Task<Guid> InsertActiveUserAsync(IServiceProvider services, string prefix)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            UserName = $"approval-{prefix}-{Guid.NewGuid():N}",
            EncryptedPassword = "test",
            Salt = "test",
            IsActive = true
        };
        await services.GetRequiredService<IUserRepository>().InsertAsync(user);
        return user.Id;
    }

    private static async Task<ApprovalTask> GetSinglePendingTaskAsync(
        IServiceScope scope,
        Guid instanceId,
        Guid assigneeUserId)
    {
        var tasks = await scope.ServiceProvider.GetRequiredService<IApprovalTaskRepository>()
            .GetListAsync(item =>
                item.InstanceId == instanceId &&
                item.Status == ApprovalTaskStatus.Pending &&
                item.AssigneeUserId == assigneeUserId);
        return Assert.Single(tasks);
    }

    private static async Task<ApprovalInstance> ReloadInstanceAsync(IServiceScope scope, Guid instanceId)
    {
        var instance = await scope.ServiceProvider.GetRequiredService<IApprovalInstanceRepository>()
            .GetAsync(instanceId);
        return Assert.IsType<ApprovalInstance>(instance);
    }

    /// <summary>1×1 透明 PNG：通过文件类型校验链路的合法载荷。</summary>
    private static byte[] OnePixelPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==");

    private static IRemoteStreamContent BuildFile(byte[] content, string fileName) =>
        new RemoteStreamContent(new MemoryStream(content, writable: false), fileName, "image/png", content.LongLength);
}

/// <summary>审批引擎测试用业务描述符，由 <see cref="HostFixture"/> 在宿主启动前注册。</summary>
internal sealed class TestApprovalBusinessDescriptor : IApprovalBusinessDescriptor
{
    public string BusinessType => ApprovalEngineFlowTests.TestBusinessType;

    public string DisplayName => "审批引擎测试业务";

    public string? DefinitionCode => null;

    public IReadOnlyList<ApprovalFieldDto> Fields => [];
}

/// <summary>声明字段清单的测试用业务描述符：覆盖必填、枚举选项与不可比较的附件字段。</summary>
internal sealed class TestFieldedApprovalBusinessDescriptor : IApprovalBusinessDescriptor
{
    public string BusinessType => ApprovalEngineFlowTests.FieldedBusinessType;

    public string DisplayName => "审批字段测试业务";

    public string? DefinitionCode => null;

    public IReadOnlyList<ApprovalFieldDto> Fields =>
    [
        new()
        {
            Key = "amount",
            DisplayName = "金额",
            Type = nameof(ApprovalFieldType.Decimal),
            Required = true
        },
        new()
        {
            Key = "category",
            DisplayName = "类别",
            Type = nameof(ApprovalFieldType.Select),
            Options =
            [
                new ApprovalFieldOptionDto { Value = "travel", Label = "差旅" },
                new ApprovalFieldOptionDto { Value = "purchase", Label = "采购" }
            ]
        },
        new()
        {
            Key = "proof",
            DisplayName = "凭证",
            Type = nameof(ApprovalFieldType.Attachment)
        }
    ];
}
