using Microsoft.Extensions.Logging;
using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Application.Contracts.Events;
using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.IRepositories;
using Sim.Approval.Domain.Runtime;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.Framework.Domain.Shared.Messaging;
using Sim.Framework.Domain.Shared.Numbering;
using Sim.System.Application.Contracts.Organization;
using SqlSugar;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 审批运行时引擎 —— 实例发起、节点推进与任务操作的唯一入口。
/// <para>
/// 推进以<b>有向图 + 节点状态</b>为模型（见 <see cref="ApprovalFlowGraph"/>）：
/// 实例创建时按定义快照写入全部节点行，运行中只更新节点状态；节点就绪后按类型分派
/// （网关/开始/结束直接完成，抄送写记录，审批生成任务）。
/// 线性定义（无连线）按节点顺序合成 <c>Start → n1 → … → nN → End</c>，行为与第一阶段一致。
/// </para>
/// 状态变更、通知和业务终态事件都在同一工作单元内写入数据库；
/// 业务事件写入 Outbox 后由消息基础设施异步投递，业务处理失败不影响审批结果。
/// </summary>
public sealed class ApprovalEngine(
    IApprovalFlowDefinitionRepository definitionRepository,
    IApprovalFlowNodeRepository nodeRepository,
    IApprovalFlowEdgeRepository edgeRepository,
    IApprovalInstanceRepository instanceRepository,
    IApprovalInstanceNodeRepository instanceNodeRepository,
    IApprovalTaskRepository taskRepository,
    IApprovalRecordRepository recordRepository,
    IApprovalCcRepository ccRepository,
    IBusinessNumberGenerator numberGenerator,
    IOrganizationDirectory organizationDirectory,
    ApprovalBusinessRegistry businessRegistry,
    ApprovalApproverResolver approverResolver,
    ApprovalNotificationDispatcher notificationDispatcher,
    IMessageBus messageBus,
    ILogger<ApprovalEngine> logger)
{
    private const string AutoPassComment = "审批人与发起人相同，自动通过。";

    // ── 发起与重新提交 ────────────────────────────────────────────────────

    /// <summary>发起审批实例（同一业务对象同时只允许一个进行中的实例）。</summary>
    public async Task<ApprovalInstance> StartAsync(
        ApprovalSubmitInput input,
        Guid initiatorUserId,
        CancellationToken cancellationToken = default)
    {
        var businessType = (input.BusinessType ?? string.Empty).Trim();
        var businessId = (input.BusinessId ?? string.Empty).Trim();
        if (businessType.Length == 0 || businessId.Length == 0)
        {
            throw new BusinessException(ApprovalErrorCodes.InvalidRequest, "业务类型与业务对象标识不能为空。");
        }

        var definition = await ResolveDefinitionAsync(input.DefinitionCode, businessType);
        var graph = await LoadGraphAsync(definition);

        var descriptor = businessRegistry.GetRequired(businessType);
        var fieldValues = ApprovalFieldSchema.MergeValues(input.Context, input.Fields);
        ApprovalFieldSchema.EnsureRequiredFields(descriptor, fieldValues);

        var runningKey = BuildRunningKey(businessType, businessId);
        var tenantScope = ApprovalInstance.CurrentTenantScope();
        var running = await instanceRepository.GetQueryable()
            .Where(item => item.TenantScope == tenantScope && item.RunningKey == runningKey)
            .FirstAsync();
        if (running is not null)
        {
            throw new BusinessException(
                ApprovalErrorCodes.InstanceAlreadyRunning,
                $"业务对象 [{businessId}] 已存在进行中的审批实例 {running.Number}。");
        }

        var instance = new ApprovalInstance
        {
            Id = Guid.NewGuid(),
            TenantScope = tenantScope,
            Number = await numberGenerator.GenerateAsync(
                new NumberGenerationRequest(ApprovalConsts.InstanceNumberBusinessKey),
                cancellationToken),
            DefinitionId = definition.Id,
            DefinitionCode = definition.Code,
            DefinitionName = definition.Name,
            DefinitionVersion = definition.Version,
            BusinessType = businessType,
            BusinessId = businessId,
            Title = ResolveTitle(input.Title, definition),
            InitiatorUserId = initiatorUserId,
            InitiatorOrganizationUnitId = await organizationDirectory.FindPrimaryOrganizationUnitIdAsync(
                initiatorUserId,
                cancellationToken),
            Status = ApprovalInstanceStatus.Running,
            ContextJson = ApprovalRuntimeJson.SerializeContextSnapshot(
                ApprovalFieldSchema.BuildSnapshot(descriptor.Fields, fieldValues)),
            RunningKey = runningKey
        };

        try
        {
            await instanceRepository.InsertAsync(instance);
        }
        catch (SqlSugarException exception) when (IsRunningKeyConflict(exception.Message))
        {
            // 上面的存在性检查只覆盖常规路径；并发发起时由 (TenantScope, RunningKey) 唯一索引兜底。
            throw new BusinessException(
                ApprovalErrorCodes.InstanceAlreadyRunning,
                $"业务对象 [{businessId}] 已存在进行中的审批实例。");
        }

        var record = await RecordAsync(instance.Id, null, null, ApprovalAction.Submit, initiatorUserId, null, null);
        var nodes = await EnsureInstanceNodesAsync(instance, graph, cancellationToken);
        await AdvanceAsync(instance, definition, graph, nodes, initiatorUserId, cancellationToken);
        await SetRecordTargetAsync(record, CurrentRecordTarget(instance));
        return instance;
    }

    /// <summary>重新提交被退回的实例（复用实例，全部节点重置后从开始节点重新推进）。</summary>
    public async Task<ApprovalInstance> ResubmitAsync(
        Guid instanceId,
        string? title,
        IReadOnlyDictionary<string, string> context,
        Guid operatorUserId,
        IReadOnlyList<ApprovalFieldValueDto>? fields = null,
        CancellationToken cancellationToken = default)
    {
        var instance = await LoadRequiredInstanceAsync(instanceId, cancellationToken);
        ApprovalStateMachine.EnsureReturned(instance);
        ApprovalStateMachine.EnsureInitiator(instance, operatorUserId);

        var definition = await LoadSnapshotDefinitionAsync(instance);
        var graph = await LoadGraphAsync(definition);

        if (!string.IsNullOrWhiteSpace(title))
        {
            instance.Title = ResolveTitle(title, definition);
        }

        var descriptor = businessRegistry.FindDescriptor(instance.BusinessType);
        var fieldValues = ApprovalFieldSchema.MergeValues(context, fields);
        ApprovalFieldSchema.EnsureRequiredFields(descriptor, fieldValues);
        instance.ContextJson = ApprovalRuntimeJson.SerializeContextSnapshot(
            ApprovalFieldSchema.BuildSnapshot(descriptor?.Fields, fieldValues));
        instance.Status = ApprovalInstanceStatus.Running;
        instance.CompletedTime = null;
        instance.CurrentNodeSort = 0;
        instance.CurrentNodeKey = null;
        instance.CurrentNodeName = null;
        await PersistInstanceAsync(instance, operatorUserId, cancellationToken);

        var nodes = await EnsureInstanceNodesAsync(instance, graph, cancellationToken);
        await ResetInstanceNodesAsync(nodes, cancellationToken);

        var record = await RecordAsync(instance.Id, null, null, ApprovalAction.Resubmit, operatorUserId, null, null);
        await AdvanceAsync(instance, definition, graph, nodes, operatorUserId, cancellationToken);
        await SetRecordTargetAsync(record, CurrentRecordTarget(instance));
        return instance;
    }

    // ── 任务操作 ──────────────────────────────────────────────────────────

    /// <summary>同意当前任务并推进流程。</summary>
    public async Task ApproveAsync(
        Guid taskId,
        Guid operatorUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadTaskContextAsync(taskId, operatorUserId);
        var roundTasks = await GetRoundTasksAsync(context.Instance.Id, context.Node.NodeKey);
        EnsureTaskIsCurrent(context, roundTasks);
        await ClaimInstanceAsync(context.Instance, operatorUserId, cancellationToken);

        await FinalizeTaskAsync(context.Task, ApprovalTaskStatus.Approved, comment, operatorUserId, cancellationToken);
        var record = await RecordAsync(
            context.Instance.Id,
            context.Node.NodeKey,
            context.Node.Name,
            ApprovalAction.Approve,
            operatorUserId,
            null,
            comment,
            fromNodeKey: context.Node.NodeKey);
        await ContinueAfterTaskAsync(context, operatorUserId, cancellationToken);
        await SetRecordTargetAsync(record, CurrentRecordTarget(context.Instance));
    }

    /// <summary>
    /// 拒绝当前任务。或签节点本轮还有待办或已通过任务时仅记录该任务的拒绝，实例继续等待；
    /// 其余模式（会签、顺序审批）以及或签全员拒绝时，实例立即进入被拒绝终态。
    /// </summary>
    public async Task RejectAsync(
        Guid taskId,
        Guid operatorUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadTaskContextAsync(taskId, operatorUserId);
        var roundTasks = await GetRoundTasksAsync(context.Instance.Id, context.Node.NodeKey);
        EnsureTaskIsCurrent(context, roundTasks);
        await ClaimInstanceAsync(context.Instance, operatorUserId, cancellationToken);

        await FinalizeTaskAsync(context.Task, ApprovalTaskStatus.Rejected, comment, operatorUserId, cancellationToken);
        await RecordAsync(
            context.Instance.Id,
            context.Node.NodeKey,
            context.Node.Name,
            ApprovalAction.Reject,
            operatorUserId,
            null,
            comment,
            fromNodeKey: context.Node.NodeKey);

        if (context.Node.MultiApproveMode == ApprovalMultiApproveMode.Any)
        {
            var currentTasks = await GetRoundTasksAsync(context.Instance.Id, context.Node.NodeKey);
            if (currentTasks.Any(item => item.Status is ApprovalTaskStatus.Pending or ApprovalTaskStatus.Approved))
            {
                return;
            }
        }

        await CompleteInstanceAsync(context.Instance, ApprovalInstanceStatus.Rejected, comment, operatorUserId, cancellationToken);
    }

    /// <summary>将当前任务转交他人处理。</summary>
    public async Task TransferAsync(
        Guid taskId,
        Guid operatorUserId,
        Guid targetUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadTaskContextAsync(taskId, operatorUserId);
        ApprovalStateMachine.EnsureNodeAllows(context.Node, "transfer", context.Node.AllowTransfer);
        ApprovalStateMachine.EnsureValidTarget(operatorUserId, targetUserId);
        await EnsureActiveTargetAsync(targetUserId, cancellationToken);

        var roundTasks = await GetRoundTasksAsync(context.Instance.Id, context.Node.NodeKey);
        EnsureTaskIsCurrent(context, roundTasks);
        await ClaimInstanceAsync(context.Instance, operatorUserId, cancellationToken);

        await FinalizeTaskAsync(context.Task, ApprovalTaskStatus.Skipped, comment, operatorUserId, cancellationToken);

        var transferTask = CreateTask(
            context.Instance,
            context.Node,
            context.Task.Round,
            NextSortOrder(roundTasks),
            ApprovalTaskType.Transfer,
            targetUserId,
            originAssigneeUserId: context.Task.AssigneeUserId,
            sourceTaskId: context.Task.Id,
            dueAt: context.Task.DueAt);
        await taskRepository.InsertAsync(transferTask);
        await RecordAsync(
            context.Instance.Id,
            context.Node.NodeKey,
            context.Node.Name,
            ApprovalAction.Transfer,
            operatorUserId,
            targetUserId,
            comment);
        await notificationDispatcher.PublishTasksCreatedAsync(context.Instance, [transferTask], cancellationToken);
    }

    /// <summary>为当前任务加签（前加签先于本人审批，后加签在本人通过后审批）。</summary>
    public async Task AddSignAsync(
        Guid taskId,
        Guid operatorUserId,
        Guid targetUserId,
        ApprovalAddSignPosition position,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadTaskContextAsync(taskId, operatorUserId);
        ApprovalStateMachine.EnsureNodeAllows(context.Node, "add-sign", context.Node.AllowAddSign);
        ApprovalStateMachine.EnsureValidTarget(operatorUserId, targetUserId);
        await EnsureActiveTargetAsync(targetUserId, cancellationToken);

        var roundTasks = await GetRoundTasksAsync(context.Instance.Id, context.Node.NodeKey);
        EnsureTaskIsCurrent(context, roundTasks);
        await ClaimInstanceAsync(context.Instance, operatorUserId, cancellationToken);

        var sortOrder = NextSortOrder(roundTasks);
        var isBefore = position == ApprovalAddSignPosition.Before;
        var signTask = CreateTask(
            context.Instance,
            context.Node,
            context.Task.Round,
            sortOrder,
            isBefore ? ApprovalTaskType.AddSignBefore : ApprovalTaskType.AddSignAfter,
            targetUserId,
            originAssigneeUserId: context.Task.AssigneeUserId,
            sourceTaskId: context.Task.Id,
            dueAt: context.Task.DueAt);

        await FinalizeTaskAsync(
            context.Task,
            isBefore ? ApprovalTaskStatus.Skipped : ApprovalTaskStatus.Approved,
            comment,
            operatorUserId,
            cancellationToken);

        await taskRepository.InsertAsync(signTask);
        await RecordAsync(
            context.Instance.Id,
            context.Node.NodeKey,
            context.Node.Name,
            ApprovalAction.AddSign,
            operatorUserId,
            targetUserId,
            comment);
        await notificationDispatcher.PublishTasksCreatedAsync(context.Instance, [signTask], cancellationToken);
    }

    /// <summary>退回上一步：回到图上最近一个已完成审批的前驱节点重新审批，其后的节点全部重来。</summary>
    public async Task ReturnToPreviousAsync(
        Guid taskId,
        Guid operatorUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadTaskContextAsync(taskId, operatorUserId);
        var roundTasks = await GetRoundTasksAsync(context.Instance.Id, context.Node.NodeKey);
        EnsureTaskIsCurrent(context, roundTasks);
        await ClaimInstanceAsync(context.Instance, operatorUserId, cancellationToken);

        var previousNode = FindPreviousApprovedNode(context.Graph, context.Nodes, context.Node);
        if (previousNode is null)
        {
            throw new BusinessException(
                ApprovalErrorCodes.ReturnPreviousUnavailable,
                "当前节点之前没有已通过的审批节点，无法退回上一步。");
        }

        await FinalizeTaskAsync(context.Task, ApprovalTaskStatus.Skipped, comment, operatorUserId, cancellationToken);
        await CancelPendingTasksAsync(context.Instance.Id, context.Node.NodeKey, context.Task.Id);
        var record = await RecordAsync(
            context.Instance.Id,
            context.Node.NodeKey,
            context.Node.Name,
            ApprovalAction.ReturnToPrevious,
            operatorUserId,
            null,
            comment,
            fromNodeKey: context.Node.NodeKey);

        await ResetNodeSubgraphAsync(context, previousNode.NodeKey, cancellationToken);
        await AdvanceAsync(
            context.Instance,
            context.Definition,
            context.Graph,
            context.Nodes,
            operatorUserId,
            cancellationToken);
        await SetRecordTargetAsync(record, CurrentRecordTarget(context.Instance));
    }

    /// <summary>退回发起人（实例进入被退回状态，等待发起人重新提交）。</summary>
    public async Task ReturnToInitiatorAsync(
        Guid taskId,
        Guid operatorUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadTaskContextAsync(taskId, operatorUserId);
        ApprovalStateMachine.EnsureNodeAllows(context.Node, "return", context.Node.AllowReturnToInitiator);

        var roundTasks = await GetRoundTasksAsync(context.Instance.Id, context.Node.NodeKey);
        EnsureTaskIsCurrent(context, roundTasks);
        await ClaimInstanceAsync(context.Instance, operatorUserId, cancellationToken);

        await FinalizeTaskAsync(context.Task, ApprovalTaskStatus.Skipped, comment, operatorUserId, cancellationToken);
        await RecordAsync(
            context.Instance.Id,
            context.Node.NodeKey,
            context.Node.Name,
            ApprovalAction.ReturnToInitiator,
            operatorUserId,
            null,
            comment,
            fromNodeKey: context.Node.NodeKey);

        await CancelPendingTasksAsync(context.Instance.Id, null, context.Task.Id);

        // 保留 RunningKey：实例仍占用该业务对象，等待发起人重新提交或放弃
        context.Instance.Status = ApprovalInstanceStatus.Returned;
        context.Instance.CompletedTime = DateTime.Now;
        await PersistInstanceAsync(context.Instance, operatorUserId, cancellationToken);
        await notificationDispatcher.PublishFinishedAsync(context.Instance, comment, cancellationToken);
        logger.LogInformation("审批实例 {Number} 被退回发起人", context.Instance.Number);
    }

    /// <summary>撤回实例（仅发起人、进行中或被退回、未超出撤回时间窗）。</summary>
    public async Task WithdrawAsync(
        Guid instanceId,
        Guid operatorUserId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var instance = await LoadRequiredInstanceAsync(instanceId, cancellationToken);
        var definition = await LoadSnapshotDefinitionAsync(instance);
        ApprovalStateMachine.EnsureCanWithdraw(instance, definition, operatorUserId);

        var graph = await LoadGraphAsync(definition);
        var node = graph.FindNode(instance.CurrentNodeKey);
        await RecordAsync(
            instance.Id,
            node?.NodeKey,
            node?.Name,
            ApprovalAction.Withdraw,
            operatorUserId,
            null,
            comment,
            fromNodeKey: node?.NodeKey);
        await CompleteInstanceAsync(instance, ApprovalInstanceStatus.Withdrawn, comment, operatorUserId, cancellationToken);
    }

    /// <summary>
    /// 强制终止实例（管理端或发起人上级）。进行中或被退回的实例均可终止；
    /// <paramref name="hasManagePermission"/> 为 false 时要求操作人在发起人的组织负责人链中。
    /// </summary>
    public async Task TerminateAsync(
        Guid instanceId,
        Guid operatorUserId,
        string reason,
        CancellationToken cancellationToken = default,
        bool hasManagePermission = false)
    {
        var instance = await LoadRequiredInstanceAsync(instanceId, cancellationToken);
        if (instance.Status is not (ApprovalInstanceStatus.Running or ApprovalInstanceStatus.Returned))
        {
            throw new BusinessException(
                ApprovalErrorCodes.InstanceNotRunning,
                $"实例当前状态为 {instance.Status}，不可终止。");
        }

        if (!hasManagePermission)
        {
            var managerChain = await organizationDirectory.FindManagerChainAsync(
                instance.InitiatorUserId,
                instance.InitiatorOrganizationUnitId,
                cancellationToken);
            if (!managerChain.Contains(operatorUserId))
            {
                throw new BusinessException(
                    ApprovalErrorCodes.TerminateNotAllowed,
                    "只有具备终止权限的管理员或发起人的上级可以终止该实例。");
            }
        }

        var definition = await LoadSnapshotDefinitionAsync(instance);
        var graph = await LoadGraphAsync(definition);
        var node = graph.FindNode(instance.CurrentNodeKey);
        await RecordAsync(
            instance.Id,
            node?.NodeKey,
            node?.Name,
            ApprovalAction.Terminate,
            operatorUserId,
            null,
            reason,
            fromNodeKey: node?.NodeKey);
        await CompleteInstanceAsync(instance, ApprovalInstanceStatus.Terminated, reason, operatorUserId, cancellationToken);
    }

    /// <summary>催办当前处理人（仅发起人；同一实例同一操作人 1 小时内只能催办一次）。</summary>
    public async Task UrgeAsync(Guid instanceId, Guid operatorUserId, CancellationToken cancellationToken = default)
    {
        var instance = await LoadRequiredInstanceAsync(instanceId, cancellationToken);
        ApprovalStateMachine.EnsureRunning(instance);
        ApprovalStateMachine.EnsureInitiator(instance, operatorUserId);

        var urgeThreshold = DateTime.Now.AddHours(-ApprovalConsts.UrgeIntervalHours);
        var recentUrges = await recordRepository.GetListAsync(
            item => item.InstanceId == instance.Id
                    && item.Action == ApprovalAction.Urge
                    && item.OperatorUserId == operatorUserId
                    && item.CreationTime > urgeThreshold);
        if (recentUrges.Count > 0)
        {
            throw new BusinessException(
                ApprovalErrorCodes.UrgeTooFrequent,
                $"同一实例 {ApprovalConsts.UrgeIntervalHours} 小时内只能催办一次。");
        }

        var pendingTasks = await taskRepository.GetListAsync(
            item => item.InstanceId == instance.Id && item.Status == ApprovalTaskStatus.Pending);
        if (pendingTasks.Count == 0)
        {
            throw new BusinessException(ApprovalErrorCodes.TaskNotFound, "实例当前没有待处理的任务。");
        }

        await RecordAsync(
            instance.Id,
            pendingTasks[0].NodeKey,
            pendingTasks[0].NodeName,
            ApprovalAction.Urge,
            operatorUserId,
            null,
            null);
        await notificationDispatcher.PublishUrgedAsync(instance, pendingTasks, cancellationToken);
    }

    /// <summary>标记抄送记录已读（仅接收人本人）。</summary>
    public async Task MarkCcReadAsync(Guid ccId, Guid operatorUserId, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var cc = await ccRepository.GetAsync(ccId)
            ?? throw new BusinessException(ApprovalErrorCodes.InstanceNotFound, $"抄送记录 {ccId} 不存在。");
        if (cc.UserId != operatorUserId)
        {
            throw new BusinessException(ApprovalErrorCodes.NotTaskAssignee, "只能标记自己的抄送记录。");
        }

        if (cc.ReadAt.HasValue)
        {
            return;
        }

        cc.ReadAt = DateTime.Now;
        await ccRepository.UpdateAsync(cc);
    }

    /// <summary>查询实例当前待处理的审批人（业务模块发起后展示跟进人）。</summary>
    public async Task<List<Guid>> GetCurrentPendingAssigneeIdsAsync(
        Guid instanceId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var tasks = await taskRepository.GetListAsync(
            item => item.InstanceId == instanceId && item.Status == ApprovalTaskStatus.Pending);
        return tasks.Select(item => item.AssigneeUserId).Distinct().ToList();
    }

    // ── 节点推进 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 推进实例：反复执行「死分支传播 → 激活就绪节点」，直到没有节点状态发生变化；
    /// 随后更新实例当前节点，若已无未完成的节点则实例通过。
    /// </summary>
    private async Task AdvanceAsync(
        ApprovalInstance instance,
        ApprovalFlowDefinition definition,
        ApprovalFlowGraph graph,
        IReadOnlyDictionary<string, ApprovalInstanceNode> nodes,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var context = ApprovalRuntimeJson.DeserializeContextValues(instance.ContextJson);

        while (true)
        {
            var changed = false;

            foreach (var dead in graph.ResolveDeadBranches(SnapshotStates(nodes)))
            {
                await SetNodeStateAsync(nodes[dead.NodeKey], ApprovalNodeState.Skipped, cancellationToken);
                changed = true;
            }

            var ready = graph.Nodes.Where(node => graph.IsReady(node, SnapshotStates(nodes))).ToList();
            foreach (var node in ready)
            {
                if (!ApprovalConditionEvaluator.IsActive(node.ConditionJson, context))
                {
                    // 节点自身条件不成立表示该节点免审，但流程仍须沿出边继续。
                    // 分支未选中只由网关出边剪枝表达，不能把两种语义混在一起。
                    await CompleteNodeAsync(
                        graph,
                        nodes,
                        node,
                        nodes[node.NodeKey],
                        context,
                        cancellationToken);
                    changed = true;
                    continue;
                }

                await ActivateNodeAsync(
                    instance,
                    definition,
                    graph,
                    nodes,
                    node,
                    actorUserId,
                    cancellationToken);
                changed = true;
            }

            if (!changed)
            {
                break;
            }
        }

        var active = graph.Nodes
            .Where(node => nodes[node.NodeKey].State == ApprovalNodeState.Active)
            .ToList();
        var pending = graph.Nodes
            .Where(node => nodes[node.NodeKey].State == ApprovalNodeState.Pending)
            .ToList();
        if (active.Count == 0 && pending.Count == 0)
        {
            if (!await HasApprovalWorkInCurrentRunAsync(instance.Id, graph, nodes))
            {
                throw new BusinessException(
                    ApprovalErrorCodes.DefinitionInvalid,
                    "流程没有激活任何审批节点，请检查节点条件与分支配置。");
            }

            await CompleteInstanceAsync(
                instance,
                ApprovalInstanceStatus.Approved,
                null,
                actorUserId,
                cancellationToken);
            return;
        }

        await UpdateCurrentNodeAsync(instance, active, actorUserId, cancellationToken);
    }

    /// <summary>按节点类型激活：开始/网关/结束直接完成，抄送写记录，审批生成任务。</summary>
    private async Task ActivateNodeAsync(
        ApprovalInstance instance,
        ApprovalFlowDefinition definition,
        ApprovalFlowGraph graph,
        IReadOnlyDictionary<string, ApprovalInstanceNode> nodes,
        ApprovalFlowNode node,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var context = ApprovalRuntimeJson.DeserializeContextValues(instance.ContextJson);
        var row = nodes[node.NodeKey];

        switch (node.NodeType)
        {
            case ApprovalNodeType.Start:
            case ApprovalNodeType.ExclusiveGateway:
            case ApprovalNodeType.ParallelGateway:
            case ApprovalNodeType.End:
                await CompleteNodeAsync(graph, nodes, node, row, context, cancellationToken);
                return;

            case ApprovalNodeType.Cc:
                await CreateCcRecordsAsync(instance, node, cancellationToken);
                await CompleteNodeAsync(graph, nodes, node, row, context, cancellationToken);
                return;
        }

        var existingTasks = await GetRoundTasksForAllRoundsAsync(instance.Id, node.NodeKey);
        var completed = await ActivateApprovalNodeAsync(
            instance,
            definition,
            node,
            row,
            existingTasks,
            actorUserId,
            cancellationToken);
        if (completed)
        {
            await CompleteNodeAsync(graph, nodes, node, row, context, cancellationToken);
            return;
        }

        await SetNodeStateAsync(row, ApprovalNodeState.Active, cancellationToken);
    }

    /// <summary>激活审批节点并创建任务；返回 true 表示无需等待（审批人全部自动通过）。</summary>
    private async Task<bool> ActivateApprovalNodeAsync(
        ApprovalInstance instance,
        ApprovalFlowDefinition definition,
        ApprovalFlowNode node,
        ApprovalInstanceNode row,
        IReadOnlyList<ApprovalTask> existingTasks,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        _ = actorUserId;
        var round = existingTasks.Count == 0 ? 1 : existingTasks.Max(item => item.Round) + 1;
        var dueAt = ResolveDueAt(node);
        if (row.Round < round)
        {
            row.Round = round;
        }

        if (node.MultiApproveMode == ApprovalMultiApproveMode.Sequential)
        {
            var activated = await ActivateSequentialAsync(
                instance, definition, node, existingTasks, round, dueAt, cancellationToken);
            if (activated)
            {
                return false;
            }

            var currentRoundTasks = await GetRoundTasksAsync(instance.Id, node.NodeKey);
            if (currentRoundTasks.Any(item => item.Status == ApprovalTaskStatus.Approved))
            {
                return true;
            }

            throw new BusinessException(
                ApprovalErrorCodes.ApproverUnresolved,
                $"节点 [{node.Name}] 没有可激活的审批人。");
        }

        var assignees = await approverResolver.ResolveAsync(
            node, instance, definition.FallbackApproverUserIds, cancellationToken);
        var sortOrder = 0;
        var pendingTasks = new List<ApprovalTask>();
        foreach (var assignee in assignees)
        {
            sortOrder++;
            if (ShouldAutoPass(instance, definition, assignee))
            {
                await CreateAutoPassTaskAsync(instance, node, round, sortOrder, assignee, dueAt);
                continue;
            }

            pendingTasks.Add(CreateTask(
                instance, node, round, sortOrder, ApprovalTaskType.Approval, assignee, dueAt: dueAt));
        }

        if (pendingTasks.Count == 0)
        {
            return true;
        }

        await taskRepository.InsertManyAsync(pendingTasks);
        await notificationDispatcher.PublishTasksCreatedAsync(instance, pendingTasks, cancellationToken);
        return false;
    }

    /// <summary>顺序审批：激活下一个未处理的审批人；返回 false 表示已无待激活审批人。</summary>
    private async Task<bool> ActivateSequentialAsync(
        ApprovalInstance instance,
        ApprovalFlowDefinition definition,
        ApprovalFlowNode node,
        IReadOnlyList<ApprovalTask> existingTasks,
        int round,
        DateTime? dueAt,
        CancellationToken cancellationToken)
    {
        var roundTasks = existingTasks.Where(item => item.Round == round).ToList();
        var processedUsers = BuildProcessedUsers(roundTasks);
        var sortOrder = roundTasks.Count == 0 ? 0 : roundTasks.Max(item => item.SortOrder);

        var assignees = await approverResolver.ResolveAsync(
            node, instance, definition.FallbackApproverUserIds, cancellationToken);
        foreach (var assignee in assignees)
        {
            if (processedUsers.Contains(assignee))
            {
                continue;
            }

            sortOrder++;
            if (ShouldAutoPass(instance, definition, assignee))
            {
                await CreateAutoPassTaskAsync(instance, node, round, sortOrder, assignee, dueAt);
                continue;
            }

            var task = CreateTask(instance, node, round, sortOrder, ApprovalTaskType.Approval, assignee, dueAt: dueAt);
            await taskRepository.InsertAsync(task);
            await notificationDispatcher.PublishTasksCreatedAsync(instance, [task], cancellationToken);
            return true;
        }

        return false;
    }

    /// <summary>任务处理后的节点收尾：前加签恢复、或签跳过、顺序审批续档、节点完成则推进。</summary>
    private async Task ContinueAfterTaskAsync(
        TaskContext context,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var task = context.Task;
        var node = context.Node;

        // 前加签及其后续转交链完成后恢复原审批任务。
        if (await TryResumeBeforeSignSourceAsync(context.Instance, task, cancellationToken))
        {
            return;
        }

        var roundTasks = await GetRoundTasksAsync(context.Instance.Id, node.NodeKey);

        // 或签：首个通过后跳过本轮其余待处理任务（本次任务派生的加签任务除外）
        if (node.MultiApproveMode == ApprovalMultiApproveMode.Any)
        {
            var skipped = roundTasks
                .Where(item => item.Status == ApprovalTaskStatus.Pending
                               && item.Id != task.Id
                               && item.SourceTaskId != task.Id)
                .ToList();
            if (skipped.Count > 0)
            {
                var now = DateTime.Now;
                foreach (var item in skipped)
                {
                    item.Status = ApprovalTaskStatus.Skipped;
                    item.HandledTime = now;
                }

                await taskRepository.UpdateManyAsync(skipped);
            }
        }

        // 顺序审批：继续激活下一位审批人
        if (node.MultiApproveMode == ApprovalMultiApproveMode.Sequential)
        {
            var activated = await ActivateSequentialAsync(
                context.Instance,
                context.Definition,
                node,
                roundTasks,
                task.Round,
                ResolveDueAt(node),
                cancellationToken);
            if (activated)
            {
                return;
            }
        }

        roundTasks = await GetRoundTasksAsync(context.Instance.Id, node.NodeKey);
        if (!IsNodeComplete(roundTasks))
        {
            return;
        }

        var contextValues = ApprovalRuntimeJson.DeserializeContextValues(context.Instance.ContextJson);
        await CompleteNodeAsync(
            context.Graph,
            context.Nodes,
            node,
            context.Nodes[node.NodeKey],
            contextValues,
            cancellationToken);
        await AdvanceAsync(
            context.Instance,
            context.Definition,
            context.Graph,
            context.Nodes,
            actorUserId,
            cancellationToken);
    }

    /// <summary>沿任务来源链查找前加签，并在加签链完成后恢复原审批任务。</summary>
    private async Task<bool> TryResumeBeforeSignSourceAsync(
        ApprovalInstance instance,
        ApprovalTask handledTask,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var current = handledTask;
        var visited = new HashSet<Guid>();
        while (visited.Add(current.Id))
        {
            if (current.TaskType == ApprovalTaskType.AddSignBefore && current.SourceTaskId is { } originalTaskId)
            {
                var original = await taskRepository.GetAsync(originalTaskId);
                if (original is not null && original.Status == ApprovalTaskStatus.Skipped)
                {
                    original.Status = ApprovalTaskStatus.Pending;
                    original.HandledTime = null;
                    await taskRepository.UpdateAsync(original);
                    await notificationDispatcher.PublishTasksCreatedAsync(instance, [original], cancellationToken);
                }

                return true;
            }

            if (current.SourceTaskId is not { } sourceTaskId)
            {
                return false;
            }

            var source = await taskRepository.GetAsync(sourceTaskId);
            if (source is null || source.InstanceId != instance.Id)
            {
                return false;
            }

            current = source;
        }

        return false;
    }

    /// <summary>当前推进轮次必须至少生成过一条审批任务，防止所有审批节点被条件绕过后自动通过。</summary>
    private async Task<bool> HasApprovalWorkInCurrentRunAsync(
        Guid instanceId,
        ApprovalFlowGraph graph,
        IReadOnlyDictionary<string, ApprovalInstanceNode> nodes)
    {
        var approvalNodeKeys = graph.Nodes
            .Where(item => item.NodeType == ApprovalNodeType.Approval)
            .Select(item => item.NodeKey)
            .ToHashSet(StringComparer.Ordinal);
        if (approvalNodeKeys.Count == 0)
        {
            return false;
        }

        var tasks = await taskRepository.GetListAsync(item => item.InstanceId == instanceId);
        return tasks.Any(task =>
            approvalNodeKeys.Contains(task.NodeKey)
            && nodes.TryGetValue(task.NodeKey, out var row)
            && task.Round == row.Round);
    }

    /// <summary>节点完成：置 <c>Completed</c> 并剪枝未选中的出边分支。</summary>
    private async Task CompleteNodeAsync(
        ApprovalFlowGraph graph,
        IReadOnlyDictionary<string, ApprovalInstanceNode> nodes,
        ApprovalFlowNode node,
        ApprovalInstanceNode row,
        IReadOnlyDictionary<string, string> context,
        CancellationToken cancellationToken)
    {
        await SetNodeStateAsync(row, ApprovalNodeState.Completed, cancellationToken);
        foreach (var pruned in graph.ResolvePrunedTargets(node, SnapshotStates(nodes), context))
        {
            await SetNodeStateAsync(nodes[pruned.NodeKey], ApprovalNodeState.Skipped, cancellationToken);
        }
    }

    /// <summary>生成抄送记录（按节点幂等，重复激活不重复抄送）。</summary>
    private async Task CreateCcRecordsAsync(
        ApprovalInstance instance,
        ApprovalFlowNode node,
        CancellationToken cancellationToken)
    {
        var configuration = ApprovalRuntimeJson.DeserializeCcConfig(node.CcConfigJson);
        var userIds = new HashSet<Guid>(configuration.UserIds.Where(item => item != Guid.Empty));
        foreach (var roleName in configuration.Roles.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            foreach (var userId in await organizationDirectory.FindUserIdsByRoleNameAsync(roleName, cancellationToken))
            {
                userIds.Add(userId);
            }
        }

        if (configuration.Initiator)
        {
            userIds.Add(instance.InitiatorUserId);
        }

        if (userIds.Count == 0)
        {
            return;
        }

        var existing = await ccRepository.GetListAsync(
            item => item.InstanceId == instance.Id && item.NodeKey == node.NodeKey);
        var existingUsers = existing.Select(item => item.UserId).ToHashSet();
        var created = new List<Guid>();
        foreach (var userId in userIds.Where(item => !existingUsers.Contains(item)))
        {
            await ccRepository.InsertAsync(new ApprovalCc
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                NodeKey = node.NodeKey,
                UserId = userId
            });
            created.Add(userId);
        }

        if (created.Count > 0)
        {
            await notificationDispatcher.PublishCcCreatedAsync(instance, created, node.Name, cancellationToken);
            await RecordAsync(
                instance.Id,
                node.NodeKey,
                node.Name,
                ApprovalAction.Cc,
                null,
                null,
                null,
                fromNodeKey: node.NodeKey);
        }
    }

    /// <summary>
    /// 实例进入终态：剩余节点置跳过、结束当前待办、释放业务对象占位、通知发起人并回调业务模块。
    /// </summary>
    private async Task CompleteInstanceAsync(
        ApprovalInstance instance,
        ApprovalInstanceStatus status,
        string? comment,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        instance.Status = status;
        instance.CompletedTime = DateTime.Now;
        instance.RunningKey = null;
        await PersistInstanceAsync(instance, actorUserId, cancellationToken);

        await CancelPendingTasksAsync(instance.Id, null, null);
        await SkipOpenInstanceNodesAsync(instance.Id);
        await notificationDispatcher.PublishFinishedAsync(instance, comment, cancellationToken);
        await PublishCompletedEventAsync(instance, status, comment, cancellationToken);
        logger.LogInformation("审批实例 {Number} 进入终态 {Status}", instance.Number, status);
    }

    /// <summary>终态业务事件写入 Outbox；投递与业务消费由消息基础设施异步完成。</summary>
    private async Task PublishCompletedEventAsync(
        ApprovalInstance instance,
        ApprovalInstanceStatus status,
        string? comment,
        CancellationToken cancellationToken)
    {
        var message = new ApprovalInstanceCompletedEvent
        {
            InstanceId = instance.Id,
            Number = instance.Number,
            DefinitionCode = instance.DefinitionCode,
            DefinitionName = instance.DefinitionName,
            DefinitionVersion = instance.DefinitionVersion,
            BusinessType = instance.BusinessType,
            BusinessId = instance.BusinessId,
            Title = instance.Title,
            InitiatorUserId = instance.InitiatorUserId,
            Status = status.ToString(),
            Comment = comment,
            CompletedTime = instance.CompletedTime ?? DateTime.Now,
            Context = ApprovalRuntimeJson.DeserializeContextValues(instance.ContextJson)
        };

        await messageBus.PublishAsync(
            message,
            new MessagePublishOptions
            {
                Type = ApprovalConsts.IntegrationEventInstanceCompleted,
                CorrelationId = instance.Id.ToString("D"),
                TenantId = instance.TenantId,
                PartitionKey = instance.BusinessType
            },
            cancellationToken);

        logger.LogDebug(
            "审批实例 {Number} 终态事件已写入 Outbox（{Status}）",
            instance.Number,
            status);
    }

    /// <summary>完成节点创建时的自动通过任务与审计记录。</summary>
    private async Task CreateAutoPassTaskAsync(
        ApprovalInstance instance,
        ApprovalFlowNode node,
        int round,
        int sortOrder,
        Guid assigneeUserId,
        DateTime? dueAt)
    {
        var autoTask = CreateTask(
            instance,
            node,
            round,
            sortOrder,
            ApprovalTaskType.Approval,
            assigneeUserId,
            dueAt: dueAt,
            status: ApprovalTaskStatus.Approved,
            comment: AutoPassComment,
            handledTime: DateTime.Now);
        await taskRepository.InsertAsync(autoTask);
        await RecordAsync(
            instance.Id,
            node.NodeKey,
            node.Name,
            ApprovalAction.AutoPass,
            assigneeUserId,
            null,
            AutoPassComment,
            fromNodeKey: node.NodeKey);
    }

    // ── 节点状态持久化 ────────────────────────────────────────────────────

    /// <summary>按定义快照加载运行时图（线性定义在此合成开始/结束节点）。</summary>
    private async Task<ApprovalFlowGraph> LoadGraphAsync(ApprovalFlowDefinition definition)
    {
        var nodes = await LoadNodesAsync(definition.Id);
        var edges = await edgeRepository.GetListAsync(item => item.DefinitionId == definition.Id);
        return ApprovalFlowGraph.Build(definition.Id, nodes, edges);
    }

    /// <summary>加载实例节点行；缺失的行按既有任务、抄送与当前节点推导后补齐。</summary>
    private async Task<Dictionary<string, ApprovalInstanceNode>> EnsureInstanceNodesAsync(
        ApprovalInstance instance,
        ApprovalFlowGraph graph,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var existing = await instanceNodeRepository.GetListAsync(item => item.InstanceId == instance.Id);
        var rows = existing.ToDictionary(item => item.NodeKey, item => item, StringComparer.Ordinal);
        var missing = graph.Nodes.Where(node => !rows.ContainsKey(node.NodeKey)).ToList();
        if (missing.Count == 0)
        {
            return rows;
        }

        var tasks = await taskRepository.GetListAsync(item => item.InstanceId == instance.Id);
        var ccs = await ccRepository.GetListAsync(item => item.InstanceId == instance.Id);
        var context = ApprovalRuntimeJson.DeserializeContextValues(instance.ContextJson);
        var now = DateTime.Now;

        var created = new List<ApprovalInstanceNode>(missing.Count);
        foreach (var node in missing)
        {
            var state = DeriveNodeState(node, instance, tasks, ccs, context);
            var nodeTasks = tasks.Where(item => item.NodeKey == node.NodeKey).ToList();
            created.Add(new ApprovalInstanceNode
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                NodeKey = node.NodeKey,
                NodeName = node.Name,
                NodeType = node.NodeType,
                SortOrder = node.SortOrder,
                State = state,
                ActivatedTime = state == ApprovalNodeState.Pending ? null : now,
                CompletedTime = state.IsSettled() ? now : null,
                Round = nodeTasks.Count == 0 ? 1 : nodeTasks.Max(item => item.Round)
            });
        }

        await instanceNodeRepository.InsertManyAsync(created);
        foreach (var row in created)
        {
            rows[row.NodeKey] = row;
        }

        return rows;
    }

    /// <summary>
    /// 升级前创建的实例没有节点行：按既有任务、抄送与实例当前节点推导，
    /// 避免把已完成的节点当作未到达而重复审批。
    /// </summary>
    private static ApprovalNodeState DeriveNodeState(
        ApprovalFlowNode node,
        ApprovalInstance instance,
        IReadOnlyList<ApprovalTask> tasks,
        IReadOnlyList<ApprovalCc> ccs,
        IReadOnlyDictionary<string, string> context)
    {
        var nodeTasks = tasks.Where(item => item.NodeKey == node.NodeKey).ToList();
        if (nodeTasks.Any(item => item.Status == ApprovalTaskStatus.Pending))
        {
            return ApprovalNodeState.Active;
        }

        if (nodeTasks.Count > 0 ||
            ccs.Any(item => item.NodeKey == node.NodeKey) ||
            node.NodeType == ApprovalNodeType.Start)
        {
            return ApprovalNodeState.Completed;
        }

        if (string.Equals(instance.CurrentNodeKey, node.NodeKey, StringComparison.Ordinal))
        {
            return ApprovalNodeState.Active;
        }

        if (node.SortOrder < instance.CurrentNodeSort)
        {
            return ApprovalConditionEvaluator.IsActive(node.ConditionJson, context)
                ? ApprovalNodeState.Completed
                : ApprovalNodeState.Skipped;
        }

        return ApprovalNodeState.Pending;
    }

    private static Dictionary<string, ApprovalNodeState> SnapshotStates(
        IReadOnlyDictionary<string, ApprovalInstanceNode> nodes) =>
        nodes.ToDictionary(item => item.Key, item => item.Value.State, StringComparer.Ordinal);

    private async Task SetNodeStateAsync(
        ApprovalInstanceNode row,
        ApprovalNodeState state,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (row.State == state)
        {
            return;
        }

        var now = DateTime.Now;
        if (state != ApprovalNodeState.Pending && row.ActivatedTime is null)
        {
            row.ActivatedTime = now;
        }

        row.State = state;
        row.CompletedTime = state.IsSettled() ? now : null;
        await instanceNodeRepository.UpdateAsync(row);
    }

    /// <summary>重置实例全部节点（重新提交时使用）：回到未到达并进入新一轮。</summary>
    private async Task ResetInstanceNodesAsync(
        IReadOnlyDictionary<string, ApprovalInstanceNode> nodes,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var rows = nodes.Values.ToList();
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            row.State = ApprovalNodeState.Pending;
            row.ActivatedTime = null;
            row.CompletedTime = null;
            row.Round += 1;
        }

        await instanceNodeRepository.UpdateManyAsync(rows);
    }

    /// <summary>把从指定节点出发可达的节点子图重置为未到达（退回上一步时使用）。</summary>
    private async Task ResetNodeSubgraphAsync(
        TaskContext context,
        string startNodeKey,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var reachable = new List<ApprovalInstanceNode>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { startNodeKey };
        var queue = new Queue<string>();
        queue.Enqueue(startNodeKey);
        while (queue.Count > 0)
        {
            var key = queue.Dequeue();
            if (context.Nodes.TryGetValue(key, out var row))
            {
                reachable.Add(row);
            }

            foreach (var edge in context.Graph.GetOutgoing(key))
            {
                if (visited.Add(edge.ToNodeKey))
                {
                    queue.Enqueue(edge.ToNodeKey);
                }
            }
        }

        if (reachable.Count == 0)
        {
            return;
        }

        foreach (var row in reachable)
        {
            row.State = ApprovalNodeState.Pending;
            row.ActivatedTime = null;
            row.CompletedTime = null;
            row.Round += 1;
        }

        await instanceNodeRepository.UpdateManyAsync(reachable);
    }

    /// <summary>实例终态时把未完成的节点置为跳过。</summary>
    private async Task SkipOpenInstanceNodesAsync(Guid instanceId)
    {
        var rows = await instanceNodeRepository.GetListAsync(item => item.InstanceId == instanceId);
        var open = rows
            .Where(item => item.State is ApprovalNodeState.Pending or ApprovalNodeState.Active)
            .ToList();
        if (open.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var row in open)
        {
            row.State = ApprovalNodeState.Skipped;
            row.CompletedTime = now;
        }

        await instanceNodeRepository.UpdateManyAsync(open);
    }

    /// <summary>更新实例当前节点（并行的多个活动节点取执行顺序最靠前的一个）。</summary>
    private async Task UpdateCurrentNodeAsync(
        ApprovalInstance instance,
        IReadOnlyList<ApprovalFlowNode> activeNodes,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var current = activeNodes.OrderBy(item => item.SortOrder).FirstOrDefault();
        if (current is null)
        {
            return;
        }

        if (string.Equals(instance.CurrentNodeKey, current.NodeKey, StringComparison.Ordinal) &&
            instance.CurrentNodeSort == current.SortOrder)
        {
            return;
        }

        instance.CurrentNodeSort = current.SortOrder;
        instance.CurrentNodeKey = current.NodeKey;
        instance.CurrentNodeName = current.Name;
        await PersistInstanceAsync(instance, actorUserId, cancellationToken);
    }

    // ── 并发控制 ──────────────────────────────────────────────────────────

    /// <summary>抢占实例推进权，使转交、加签及会签中的部分完成也参与实例版本竞争。</summary>
    private Task ClaimInstanceAsync(
        ApprovalInstance instance,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        PersistInstanceAsync(instance, actorUserId, cancellationToken);

    private async Task EnsureActiveTargetAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        if (!await organizationDirectory.IsActiveUserAsync(targetUserId, cancellationToken))
        {
            throw new BusinessException(
                ApprovalErrorCodes.InvalidTargetUser,
                "目标用户不存在、已停用或不属于当前租户。");
        }
    }

    /// <summary>
    /// 原子推进实例状态：以库中 <c>Version</c> 为条件写入，成功后实例版本号递增。
    /// 版本已被其他操作推进时抛出 <see cref="ApprovalErrorCodes.ConcurrentModified"/>。
    /// </summary>
    private async Task PersistInstanceAsync(
        ApprovalInstance instance,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var expectedVersion = instance.Version;
        instance.Version = expectedVersion + 1;
        var affected = await instanceRepository.TryAdvanceAsync(instance, expectedVersion, actorUserId);
        if (affected == 0)
        {
            throw new BusinessException(
                ApprovalErrorCodes.ConcurrentModified,
                $"审批实例 {instance.Number} 已被其他操作推进，请刷新后重试。");
        }
    }

    /// <summary>
    /// 原子定案任务：仅当库中任务仍为待处理时写入结论。
    /// 已被他人定案时抛出 <see cref="ApprovalErrorCodes.ConcurrentModified"/>。
    /// </summary>
    private async Task FinalizeTaskAsync(
        ApprovalTask task,
        ApprovalTaskStatus targetStatus,
        string? comment,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var handledTime = DateTime.Now;
        var affected = await taskRepository.TryFinalizeAsync(
            task.Id,
            ApprovalTaskStatus.Pending,
            targetStatus,
            comment,
            handledTime,
            actorUserId);
        if (affected == 0)
        {
            throw new BusinessException(
                ApprovalErrorCodes.ConcurrentModified,
                "该审批任务已被其他操作处理，请刷新后重试。");
        }

        task.Status = targetStatus;
        task.Comment = comment;
        task.HandledTime = handledTime;
    }

    /// <summary>唯一索引冲突识别：PostgreSQL 报索引名，Sqlite 报“表.列”组合。</summary>
    private static bool IsRunningKeyConflict(string message) =>
        message.Contains(ApprovalConsts.RunningKeyUniqueIndex, StringComparison.Ordinal)
        || (message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            && message.Contains("approval_instance", StringComparison.OrdinalIgnoreCase));

    // ── 查询与装配辅助 ────────────────────────────────────────────────────

    /// <summary>任务操作上下文：任务、实例、定义、运行时图与实例节点状态。</summary>
    private sealed record TaskContext(
        ApprovalTask Task,
        ApprovalInstance Instance,
        ApprovalFlowDefinition Definition,
        ApprovalFlowGraph Graph,
        IReadOnlyDictionary<string, ApprovalInstanceNode> Nodes,
        ApprovalFlowNode Node);

    private async Task<TaskContext> LoadTaskContextAsync(Guid taskId, Guid operatorUserId)
    {
        var task = await taskRepository.GetAsync(taskId)
            ?? throw new BusinessException(ApprovalErrorCodes.TaskNotFound, $"审批任务 {taskId} 不存在。");
        var instance = await LoadRequiredInstanceAsync(task.InstanceId, CancellationToken.None);
        ApprovalStateMachine.EnsureRunning(instance);
        ApprovalStateMachine.EnsureTaskActionable(task, operatorUserId);

        var definition = await LoadSnapshotDefinitionAsync(instance);
        var graph = await LoadGraphAsync(definition);
        var nodes = await EnsureInstanceNodesAsync(instance, graph, CancellationToken.None);
        var node = graph.FindNode(task.NodeKey)
            ?? throw new BusinessException(ApprovalErrorCodes.NodeNotFound, $"流程节点 [{task.NodeKey}] 不存在。");
        return new TaskContext(task, instance, definition, graph, nodes, node);
    }

    private async Task<ApprovalInstance> LoadRequiredInstanceAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var instance = await instanceRepository.GetAsync(instanceId);
        return instance
            ?? throw new BusinessException(ApprovalErrorCodes.InstanceNotFound, $"审批实例 {instanceId} 不存在。");
    }

    /// <summary>按实例快照加载流程定义（实例进行中不受定义停用影响）。</summary>
    private async Task<ApprovalFlowDefinition> LoadSnapshotDefinitionAsync(ApprovalInstance instance)
    {
        var definition = await definitionRepository.GetAsync(instance.DefinitionId);
        return definition
            ?? throw new BusinessException(
                ApprovalErrorCodes.DefinitionNotFound,
                $"实例引用的流程定义 {instance.DefinitionId} 不存在。");
    }

    private async Task<List<ApprovalFlowNode>> LoadNodesAsync(Guid definitionId)
    {
        var nodes = await nodeRepository.GetListAsync(item => item.DefinitionId == definitionId);
        return nodes.OrderBy(item => item.SortOrder).ToList();
    }

    /// <summary>按编码或业务类型解析已发布流程定义（同编码取最高版本）。</summary>
    private async Task<ApprovalFlowDefinition> ResolveDefinitionAsync(string? definitionCode, string businessType)
    {
        var descriptor = businessRegistry.GetRequired(businessType);
        var code = string.IsNullOrWhiteSpace(definitionCode)
            ? descriptor.DefinitionCode?.Trim()
            : definitionCode.Trim();

        var query = definitionRepository.GetQueryable().Where(item => item.Status == ApprovalFlowStatus.Published);
        query = string.IsNullOrWhiteSpace(code)
            ? query.Where(item => item.BusinessType == businessType)
            : query.Where(item => item.Code == code);

        var definition = await query.OrderBy(item => item.Version, OrderByType.Desc).FirstAsync();
        if (definition is null)
        {
            var target = string.IsNullOrWhiteSpace(code) ? $"业务类型 [{businessType}]" : $"流程编码 [{code}]";
            throw new BusinessException(ApprovalErrorCodes.DefinitionNotFound, $"{target} 没有已发布的审批流程。");
        }

        if (!string.IsNullOrWhiteSpace(definition.BusinessType) &&
            !string.Equals(definition.BusinessType, businessType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                ApprovalErrorCodes.DefinitionNotFound,
                $"流程 [{definition.Name}] 绑定的业务类型为 [{definition.BusinessType}]，与请求的 [{businessType}] 不一致。");
        }

        return definition;
    }

    /// <summary>获取节点当前轮次（最高轮次）的任务，按节点内次序排列。</summary>
    private async Task<List<ApprovalTask>> GetRoundTasksAsync(Guid instanceId, string nodeKey)
    {
        var tasks = await GetRoundTasksForAllRoundsAsync(instanceId, nodeKey);
        if (tasks.Count == 0)
        {
            return tasks;
        }

        var round = tasks.Max(item => item.Round);
        return tasks
            .Where(item => item.Round == round)
            .OrderBy(item => item.SortOrder)
            .ToList();
    }

    private async Task<List<ApprovalTask>> GetRoundTasksForAllRoundsAsync(Guid instanceId, string nodeKey) =>
        await taskRepository.GetListAsync(item => item.InstanceId == instanceId && item.NodeKey == nodeKey);

    /// <summary>按入边反向 BFS 查找最近的已完成审批前驱节点（同层取执行顺序最靠后者）。</summary>
    private static ApprovalFlowNode? FindPreviousApprovedNode(
        ApprovalFlowGraph graph,
        IReadOnlyDictionary<string, ApprovalInstanceNode> nodes,
        ApprovalFlowNode from)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { from.NodeKey };
        var queue = new Queue<string>();
        queue.Enqueue(from.NodeKey);

        while (queue.Count > 0)
        {
            var candidates = new List<ApprovalFlowNode>();
            var levelCount = queue.Count;
            for (var index = 0; index < levelCount; index++)
            {
                var current = queue.Dequeue();
                foreach (var edge in graph.GetIncoming(current))
                {
                    if (!visited.Add(edge.FromNodeKey))
                    {
                        continue;
                    }

                    var candidate = graph.FindNode(edge.FromNodeKey);
                    if (candidate is null)
                    {
                        continue;
                    }

                    if (candidate.NodeType.IsApproval() &&
                        nodes.TryGetValue(candidate.NodeKey, out var row) &&
                        row.State == ApprovalNodeState.Completed)
                    {
                        candidates.Add(candidate);
                    }

                    queue.Enqueue(candidate.NodeKey);
                }
            }

            if (candidates.Count > 0)
            {
                return candidates.OrderByDescending(item => item.SortOrder).First();
            }
        }

        return null;
    }

    private async Task CancelPendingTasksAsync(Guid instanceId, string? nodeKey, Guid? exceptTaskId)
    {
        var pending = await taskRepository.GetListAsync(
            item => item.InstanceId == instanceId && item.Status == ApprovalTaskStatus.Pending);
        var toCancel = pending
            .Where(item => (nodeKey is null || item.NodeKey == nodeKey) && item.Id != exceptTaskId)
            .ToList();
        if (toCancel.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var item in toCancel)
        {
            item.Status = ApprovalTaskStatus.Canceled;
            item.HandledTime = now;
        }

        await taskRepository.UpdateManyAsync(toCancel);
    }

    private async Task<ApprovalRecord> RecordAsync(
        Guid instanceId,
        string? nodeKey,
        string? nodeName,
        ApprovalAction action,
        Guid? operatorUserId,
        Guid? targetUserId,
        string? comment,
        string? fromNodeKey = null,
        string? toNodeKey = null)
    {
        var record = new ApprovalRecord
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            NodeKey = nodeKey,
            NodeName = nodeName,
            FromNodeKey = fromNodeKey,
            ToNodeKey = toNodeKey,
            Action = action,
            OperatorUserId = operatorUserId,
            TargetUserId = targetUserId,
            Comment = comment
        };
        await recordRepository.InsertAsync(record);
        return record;
    }

    /// <summary>动作完成后回填流向终点（记录先落库以保证时间线顺序，终点需推进后才可知）。</summary>
    private async Task SetRecordTargetAsync(ApprovalRecord record, string? toNodeKey)
    {
        if (string.Equals(record.ToNodeKey, toNodeKey, StringComparison.Ordinal))
        {
            return;
        }

        record.ToNodeKey = toNodeKey;
        await recordRepository.UpdateAsync(record);
    }

    /// <summary>时间线终点：仅在实例仍在推进时展示当前节点，终态为空。</summary>
    private static string? CurrentRecordTarget(ApprovalInstance instance) =>
        instance.Status == ApprovalInstanceStatus.Running ? instance.CurrentNodeKey : null;

    private static ApprovalTask CreateTask(
        ApprovalInstance instance,
        ApprovalFlowNode node,
        int round,
        int sortOrder,
        ApprovalTaskType taskType,
        Guid assigneeUserId,
        Guid? originAssigneeUserId = null,
        Guid? sourceTaskId = null,
        DateTime? dueAt = null,
        ApprovalTaskStatus status = ApprovalTaskStatus.Pending,
        string? comment = null,
        DateTime? handledTime = null) => new()
    {
        Id = Guid.NewGuid(),
        InstanceId = instance.Id,
        NodeKey = node.NodeKey,
        NodeName = node.Name,
        NodeSort = node.SortOrder,
        TaskType = taskType,
        AssigneeUserId = assigneeUserId,
        OriginAssigneeUserId = originAssigneeUserId,
        SourceTaskId = sourceTaskId,
        Status = status,
        SortOrder = sortOrder,
        Round = round,
        Comment = comment,
        DueAt = dueAt,
        HandledTime = handledTime
    };

    private static int NextSortOrder(IReadOnlyCollection<ApprovalTask> roundTasks) =>
        roundTasks.Count == 0 ? 1 : roundTasks.Max(item => item.SortOrder) + 1;

    private static HashSet<Guid> BuildProcessedUsers(IEnumerable<ApprovalTask> tasks)
    {
        var users = new HashSet<Guid>();
        foreach (var task in tasks)
        {
            users.Add(task.AssigneeUserId);
            if (task.OriginAssigneeUserId is { } origin)
            {
                users.Add(origin);
            }
        }

        return users;
    }

    /// <summary>节点是否完成：本轮已有通过任务且无待处理任务。</summary>
    private static bool IsNodeComplete(IReadOnlyCollection<ApprovalTask> roundTasks) =>
        roundTasks.Any(item => item.Status == ApprovalTaskStatus.Approved) &&
        !roundTasks.Any(item => item.Status == ApprovalTaskStatus.Pending);

    private static bool ShouldAutoPass(
        ApprovalInstance instance,
        ApprovalFlowDefinition definition,
        Guid assigneeUserId) =>
        definition.AutoPassWhenInitiator && assigneeUserId == instance.InitiatorUserId;

    private static DateTime? ResolveDueAt(ApprovalFlowNode node) =>
        node.TimeoutHours is > 0 ? DateTime.Now.AddHours(node.TimeoutHours.Value) : null;

    private static string ResolveTitle(string? title, ApprovalFlowDefinition definition)
    {
        var value = (title ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            value = definition.Name;
        }

        return value.Length > ApprovalConsts.MaxTitleLength
            ? value[..ApprovalConsts.MaxTitleLength]
            : value;
    }

    private static string BuildRunningKey(string businessType, string businessId) =>
        $"{businessType}:{businessId}";

    /// <summary>任务可操作性：所在节点仍在处理中，且任务属于该节点当前轮次。</summary>
    private static void EnsureTaskIsCurrent(TaskContext context, IReadOnlyCollection<ApprovalTask> roundTasks)
    {
        var state = context.Nodes.TryGetValue(context.Task.NodeKey, out var row)
            ? row.State
            : ApprovalNodeState.Pending;
        if (state != ApprovalNodeState.Active || !roundTasks.Any(item => item.Id == context.Task.Id))
        {
            throw new BusinessException(
                ApprovalErrorCodes.TaskAlreadyHandled,
                "任务所在节点已不在处理中，请刷新后重试。");
        }
    }
}
