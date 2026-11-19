using System.Linq.Expressions;
using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.IRepositories;
using Sim.Approval.Domain.Runtime;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Application.Contracts.Dtos;
using Sim.Framework.Domain.Shared.Exceptions;
using SqlSugar;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 审批查询服务 —— 待办 / 已办 / 我发起的 / 抄送我的 / 监控列表与实例明细装配。
/// 列表按用户过滤，明细的可见范围由调用方（用户端或管理端）传入的判断决定。
/// </summary>
public sealed class ApprovalQueryService(
    IApprovalInstanceRepository instanceRepository,
    IApprovalTaskRepository taskRepository,
    IApprovalRecordRepository recordRepository,
    IApprovalCcRepository ccRepository,
    IApprovalFlowDefinitionRepository definitionRepository,
    IApprovalFlowNodeRepository nodeRepository,
    IApprovalInstanceNodeRepository instanceNodeRepository,
    ApprovalAttachmentService attachmentService,
    ApprovalBusinessRegistry businessRegistry)
{
    private const string StatusNotStarted = "NotStarted";
    private const string StatusActive = "Active";
    private const string StatusApprovedNode = "Approved";
    private const string StatusRejectedNode = "Rejected";
    private const string StatusSkippedNode = "Skipped";

    // ── 用户端列表 ────────────────────────────────────────────────────────

    /// <summary>我的待办。</summary>
    public Task<PagedResultDto<ApprovalTaskDto>> GetMyPendingAsync(Guid userId, ApprovalTaskQueryDto input) =>
        PageTasksAsync(
            userId,
            input,
            [ApprovalTaskStatus.Pending],
            item => item.CreationTime,
            withPermissions: true);

    /// <summary>我的已办（含被跳过与被取消的任务）。</summary>
    public Task<PagedResultDto<ApprovalTaskDto>> GetMyHandledAsync(Guid userId, ApprovalTaskQueryDto input) =>
        PageTasksAsync(
            userId,
            input,
            [
                ApprovalTaskStatus.Approved,
                ApprovalTaskStatus.Rejected,
                ApprovalTaskStatus.Skipped,
                ApprovalTaskStatus.Canceled
            ],
            item => item.HandledTime,
            withPermissions: false);

    /// <summary>我发起的实例。</summary>
    public async Task<PagedResultDto<ApprovalInstanceListDto>> GetMySubmittedAsync(
        Guid userId,
        ApprovalInstanceQueryDto input)
    {
        var query = instanceRepository.GetQueryable().Where(item => item.InitiatorUserId == userId);
        return await PageInstancesAsync(ApplyInstanceFilters(query, input), input);
    }

    /// <summary>抄送我的实例。</summary>
    public async Task<PagedResultDto<ApprovalCcDto>> GetMyCcAsync(Guid userId, ApprovalInstanceQueryDto input)
    {
        var instanceIds = await ResolveInstanceIdsAsync(input);
        if (instanceIds is { Count: 0 })
        {
            return new PagedResultDto<ApprovalCcDto>(0, []);
        }

        var query = ccRepository.GetQueryable().Where(item => item.UserId == userId);
        if (instanceIds is not null)
        {
            query = query.Where(item => instanceIds.Contains(item.InstanceId));
        }

        var totalCount = await query.CountAsync();
        var records = await query
            .OrderBy(item => item.CreationTime, OrderByType.Desc)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        var instances = await LoadInstancesAsync(records.Select(item => item.InstanceId));
        return new PagedResultDto<ApprovalCcDto>(
            totalCount,
            records.Select(item =>
            {
                instances.TryGetValue(item.InstanceId, out var instance);
                return new ApprovalCcDto
                {
                    Id = item.Id,
                    InstanceId = item.InstanceId,
                    NodeKey = item.NodeKey,
                    UserId = item.UserId,
                    ReadAt = item.ReadAt,
                    CreationTime = item.CreationTime,
                    InstanceNumber = instance?.Number,
                    InstanceTitle = instance?.Title,
                    InstanceStatus = instance?.Status.ToString()
                };
            }).ToList());
    }

    /// <summary>角标数量：待办、未读抄送与进行中的发起。</summary>
    public async Task<ApprovalInstanceCountsDto> GetCountsAsync(Guid userId)
    {
        var pendingCount = await taskRepository.GetQueryable()
            .Where(item => item.AssigneeUserId == userId && item.Status == ApprovalTaskStatus.Pending)
            .CountAsync();
        var unreadCcCount = await ccRepository.GetQueryable()
            .Where(item => item.UserId == userId && item.ReadAt == null)
            .CountAsync();
        var submittedRunningCount = await instanceRepository.GetQueryable()
            .Where(item => item.InitiatorUserId == userId && item.Status == ApprovalInstanceStatus.Running)
            .CountAsync();

        return new ApprovalInstanceCountsDto
        {
            PendingCount = pendingCount,
            UnreadCcCount = unreadCcCount,
            SubmittedRunningCount = submittedRunningCount
        };
    }

    // ── 管理端列表 ────────────────────────────────────────────────────────

    /// <summary>全部实例（监控）。</summary>
    public async Task<PagedResultDto<ApprovalInstanceListDto>> GetMonitorListAsync(ApprovalInstanceQueryDto input)
    {
        var query = instanceRepository.GetQueryable();
        return await PageInstancesAsync(ApplyInstanceFilters(query, input), input);
    }

    // ── 明细 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 装配实例明细。<paramref name="canMonitor"/> 为 false 时仅发起人、处理人与抄送人可见。
    /// </summary>
    public async Task<ApprovalInstanceDetailDto> GetDetailAsync(Guid instanceId, Guid viewerUserId, bool canMonitor)
    {
        var instance = await instanceRepository.GetAsync(instanceId)
            ?? throw new BusinessException(ApprovalErrorCodes.InstanceNotFound, $"审批实例 {instanceId} 不存在。");

        var tasks = await taskRepository.GetListAsync(item => item.InstanceId == instanceId);
        var ccs = await ccRepository.GetListAsync(item => item.InstanceId == instanceId);
        if (!canMonitor &&
            instance.InitiatorUserId != viewerUserId &&
            !tasks.Any(item => item.AssigneeUserId == viewerUserId) &&
            !ccs.Any(item => item.UserId == viewerUserId))
        {
            throw new BusinessException(ApprovalErrorCodes.InstanceNotFound, $"审批实例 {instanceId} 不存在。");
        }

        var definition = await definitionRepository.GetAsync(instance.DefinitionId);
        var nodes = definition is null
            ? []
            : (await nodeRepository.GetListAsync(item => item.DefinitionId == definition.Id))
                .OrderBy(item => item.SortOrder)
                .ToList();
        var instanceNodes = await instanceNodeRepository.GetListAsync(item => item.InstanceId == instanceId);
        var records = await recordRepository.GetListAsync(item => item.InstanceId == instanceId);
        var declaredFields = businessRegistry.FindDescriptor(instance.BusinessType)?.Fields;
        var attachments = await attachmentService.ListAsync(instanceId);
        var states = instanceNodes
            .GroupBy(item => item.NodeKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().State, StringComparer.Ordinal);

        var detail = new ApprovalInstanceDetailDto
        {
            Id = instance.Id,
            Number = instance.Number,
            DefinitionCode = instance.DefinitionCode,
            DefinitionName = instance.DefinitionName,
            DefinitionVersion = instance.DefinitionVersion,
            BusinessType = instance.BusinessType,
            BusinessId = instance.BusinessId,
            Title = instance.Title,
            InitiatorUserId = instance.InitiatorUserId,
            InitiatorOrganizationUnitId = instance.InitiatorOrganizationUnitId,
            Status = instance.Status.ToString(),
            CurrentNodeKey = instance.CurrentNodeKey,
            CurrentNodeName = instance.CurrentNodeName,
            CreationTime = instance.CreationTime,
            CreatorId = instance.CreatorId,
            LastModificationTime = instance.LastModificationTime,
            LastModifierId = instance.LastModifierId,
            CompletedTime = instance.CompletedTime,
            Context = ApprovalFieldSchema.ApplyDeclaredMetadata(
                ApprovalRuntimeJson.DeserializeContextSnapshot(instance.ContextJson),
                declaredFields),
            Attachments = attachments,
            CanUploadAttachment = instance.InitiatorUserId == viewerUserId
                                  && (instance.Status is ApprovalInstanceStatus.Running
                                      or ApprovalInstanceStatus.Returned),
            CanWithdraw = definition is not null
                          && ApprovalStateMachine.CanWithdraw(instance, definition, viewerUserId),
            CanResubmit = instance.Status == ApprovalInstanceStatus.Returned
                          && instance.InitiatorUserId == viewerUserId,
            Nodes = BuildNodeProgress(instanceNodes, nodes, tasks, ccs),
            Tasks = tasks
                .OrderBy(item => item.CreationTime)
                .Select(item =>
                {
                    var node = nodes.FirstOrDefault(entry => entry.NodeKey == item.NodeKey);
                    var dto = MapToTaskDto(item);
                    dto.Permissions = node is null
                        ? null
                        : BuildPermissions(
                            instance,
                            node,
                            item,
                            viewerUserId,
                            IsTaskActionable(instance, item, states),
                            HasPreviousApprovedNode(item.NodeKey, tasks));
                    return dto;
                })
                .ToList(),
            Records = records
                .OrderBy(item => item.CreationTime)
                .Select(MapToRecordDto)
                .ToList(),
            CcRecipients = ccs
                .OrderBy(item => item.CreationTime)
                .Select(item => new ApprovalCcDto
                {
                    Id = item.Id,
                    InstanceId = item.InstanceId,
                    NodeKey = item.NodeKey,
                    UserId = item.UserId,
                    ReadAt = item.ReadAt,
                    CreationTime = item.CreationTime
                })
                .ToList()
        };

        return detail;
    }

    // ── 内部实现 ──────────────────────────────────────────────────────────

    private async Task<PagedResultDto<ApprovalTaskDto>> PageTasksAsync(
        Guid userId,
        ApprovalTaskQueryDto input,
        IReadOnlyCollection<ApprovalTaskStatus> statuses,
        Expression<Func<ApprovalTask, object?>> orderBy,
        bool withPermissions)
    {
        var instanceIds = await ResolveInstanceIdsAsync(input.DefinitionCode, input.BusinessType, input.Keyword);
        if (instanceIds is { Count: 0 })
        {
            return new PagedResultDto<ApprovalTaskDto>(0, []);
        }

        var query = taskRepository.GetQueryable()
            .Where(item => item.AssigneeUserId == userId && statuses.Contains(item.Status));
        if (instanceIds is not null)
        {
            query = query.Where(item => instanceIds.Contains(item.InstanceId));
        }

        var totalCount = await query.CountAsync();
        var tasks = await query
            .OrderBy(orderBy, OrderByType.Desc)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<ApprovalTaskDto>(
            totalCount,
            await MapTaskListAsync(tasks, userId, withPermissions));
    }

    /// <summary>装配任务列表：补齐实例信息，并按需计算当前用户的可操作项。</summary>
    private async Task<List<ApprovalTaskDto>> MapTaskListAsync(
        IReadOnlyList<ApprovalTask> tasks,
        Guid viewerUserId,
        bool withPermissions)
    {
        if (tasks.Count == 0)
        {
            return [];
        }

        var instanceIds = tasks.Select(item => item.InstanceId).Distinct().ToList();
        var instances = await LoadInstancesAsync(instanceIds);

        Dictionary<Guid, List<ApprovalFlowNode>> nodesByDefinition = [];
        Dictionary<Guid, List<ApprovalTask>> tasksByInstance = [];
        Dictionary<Guid, Dictionary<string, ApprovalNodeState>> statesByInstance = [];
        if (withPermissions)
        {
            var definitionIds = instances.Values.Select(item => item.DefinitionId).Distinct().ToList();
            var nodes = await nodeRepository.GetListAsync(item => definitionIds.Contains(item.DefinitionId));
            nodesByDefinition = nodes
                .GroupBy(item => item.DefinitionId)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(item => item.SortOrder).ToList());

            var instanceTasks = await taskRepository.GetListAsync(item => instanceIds.Contains(item.InstanceId));
            tasksByInstance = instanceTasks
                .GroupBy(item => item.InstanceId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var instanceNodes = await instanceNodeRepository.GetListAsync(item => instanceIds.Contains(item.InstanceId));
            statesByInstance = instanceNodes
                .GroupBy(item => item.InstanceId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(item => item.NodeKey, item => item.State, StringComparer.Ordinal));
        }

        var result = new List<ApprovalTaskDto>(tasks.Count);
        foreach (var task in tasks)
        {
            instances.TryGetValue(task.InstanceId, out var instance);
            var dto = MapToTaskDto(task);
            dto.InstanceNumber = instance?.Number;
            dto.InstanceTitle = instance?.Title;
            dto.InstanceStatus = instance?.Status.ToString();
            dto.BusinessType = instance?.BusinessType;
            dto.BusinessId = instance?.BusinessId;

            if (withPermissions && instance is not null &&
                nodesByDefinition.TryGetValue(instance.DefinitionId, out var nodes))
            {
                var node = nodes.FirstOrDefault(item => item.NodeKey == task.NodeKey);
                tasksByInstance.TryGetValue(task.InstanceId, out var instanceTasks);
                statesByInstance.TryGetValue(task.InstanceId, out var states);
                dto.Permissions = node is null || instanceTasks is null
                    ? null
                    : BuildPermissions(
                        instance,
                        node,
                        task,
                        viewerUserId,
                        IsTaskActionable(instance, task, states),
                        HasPreviousApprovedNode(task.NodeKey, instanceTasks));
            }

            result.Add(dto);
        }

        return result;
    }

    private async Task<Dictionary<Guid, ApprovalInstance>> LoadInstancesAsync(IEnumerable<Guid> instanceIds)
    {
        var ids = instanceIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var instances = await instanceRepository.GetListAsync(item => ids.Contains(item.Id));
        return instances.ToDictionary(item => item.Id);
    }

    private static async Task<PagedResultDto<ApprovalInstanceListDto>> PageInstancesAsync(
        ISugarQueryable<ApprovalInstance> query,
        PagedAndSortedResultRequestDto input)
    {
        var totalCount = await query.CountAsync();
        var instances = await query
            .OrderBy(item => item.CreationTime, OrderByType.Desc)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<ApprovalInstanceListDto>(
            totalCount,
            instances.Select(MapToInstanceList).ToList());
    }

    private static ISugarQueryable<ApprovalInstance> ApplyInstanceFilters(
        ISugarQueryable<ApprovalInstance> query,
        ApprovalInstanceQueryDto input)
    {
        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = ParseStatus(input.Status);
            query = query.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(input.BusinessType))
        {
            var businessType = input.BusinessType.Trim();
            query = query.Where(item => item.BusinessType == businessType);
        }

        if (!string.IsNullOrWhiteSpace(input.DefinitionCode))
        {
            var definitionCode = input.DefinitionCode.Trim();
            query = query.Where(item => item.DefinitionCode == definitionCode);
        }

        if (input.InitiatorUserId is { } initiatorUserId)
        {
            query = query.Where(item => item.InitiatorUserId == initiatorUserId);
        }

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            query = query.Where(item => item.Number.Contains(keyword) || item.Title.Contains(keyword));
        }

        return query;
    }

    /// <summary>
    /// 解析实例级过滤条件对应的实例 ID 集合；无过滤条件时返回 null（调用方无需限制）。
    /// 任务与抄送列表通过该集合实现实例字段过滤。
    /// </summary>
    private async Task<List<Guid>?> ResolveInstanceIdsAsync(string? definitionCode, string? businessType, string? keyword)
    {
        var query = instanceRepository.GetQueryable();
        var hasFilter = false;

        if (!string.IsNullOrWhiteSpace(definitionCode))
        {
            var code = definitionCode.Trim();
            query = query.Where(item => item.DefinitionCode == code);
            hasFilter = true;
        }

        if (!string.IsNullOrWhiteSpace(businessType))
        {
            var type = businessType.Trim();
            query = query.Where(item => item.BusinessType == type);
            hasFilter = true;
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var text = keyword.Trim();
            query = query.Where(item => item.Number.Contains(text) || item.Title.Contains(text));
            hasFilter = true;
        }

        return hasFilter ? await query.Select(item => item.Id).ToListAsync() : null;
    }

    private async Task<List<Guid>?> ResolveInstanceIdsAsync(ApprovalInstanceQueryDto input)
    {
        var query = instanceRepository.GetQueryable();
        var hasFilter = false;

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = ParseStatus(input.Status);
            query = query.Where(item => item.Status == status);
            hasFilter = true;
        }

        if (!string.IsNullOrWhiteSpace(input.BusinessType))
        {
            var businessType = input.BusinessType.Trim();
            query = query.Where(item => item.BusinessType == businessType);
            hasFilter = true;
        }

        if (!string.IsNullOrWhiteSpace(input.DefinitionCode))
        {
            var definitionCode = input.DefinitionCode.Trim();
            query = query.Where(item => item.DefinitionCode == definitionCode);
            hasFilter = true;
        }

        if (input.InitiatorUserId is { } initiatorUserId)
        {
            query = query.Where(item => item.InitiatorUserId == initiatorUserId);
            hasFilter = true;
        }

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            var keyword = input.Keyword.Trim();
            query = query.Where(item => item.Number.Contains(keyword) || item.Title.Contains(keyword));
            hasFilter = true;
        }

        return hasFilter ? await query.Select(item => item.Id).ToListAsync() : null;
    }

    /// <summary>
    /// 装配节点进度：优先使用实例节点状态（并行汇合与死分支只有它能表达）；
    /// 升级前创建的实例没有节点行，退回按定义节点与任务推导。
    /// </summary>
    private static List<ApprovalNodeProgressDto> BuildNodeProgress(
        IReadOnlyList<ApprovalInstanceNode> instanceNodes,
        IReadOnlyList<ApprovalFlowNode> definitionNodes,
        IReadOnlyList<ApprovalTask> tasks,
        IReadOnlyList<ApprovalCc> ccs) =>
        instanceNodes.Count == 0
            ? BuildDerivedNodeProgress(definitionNodes, tasks, ccs)
            : instanceNodes
                .OrderBy(item => item.SortOrder)
                .Select(item => BuildNodeProgress(item, tasks))
                .ToList();

    private static ApprovalNodeProgressDto BuildNodeProgress(
        ApprovalInstanceNode instanceNode,
        IReadOnlyList<ApprovalTask> tasks)
    {
        var nodeTasks = tasks.Where(item => item.NodeKey == instanceNode.NodeKey).ToList();
        var progress = new ApprovalNodeProgressDto
        {
            NodeKey = instanceNode.NodeKey,
            Name = instanceNode.NodeName,
            NodeType = instanceNode.NodeType.ToString(),
            SortOrder = instanceNode.SortOrder,
            State = instanceNode.State.ToString(),
            Round = instanceNode.Round,
            Activated = instanceNode.State != ApprovalNodeState.Pending || nodeTasks.Count > 0
        };

        if (nodeTasks.Count == 0)
        {
            progress.Status = instanceNode.State switch
            {
                ApprovalNodeState.Active => StatusActive,
                ApprovalNodeState.Completed => StatusApprovedNode,
                ApprovalNodeState.Skipped => StatusSkippedNode,
                _ => StatusNotStarted
            };
            return progress;
        }

        var round = nodeTasks.Max(item => item.Round);
        var roundTasks = nodeTasks
            .Where(item => item.Round == round)
            .OrderBy(item => item.SortOrder)
            .ToList();
        progress.Round = round;
        progress.Status = roundTasks.Any(item => item.Status == ApprovalTaskStatus.Pending)
            ? StatusActive
            : roundTasks.Any(item => item.Status == ApprovalTaskStatus.Rejected)
                ? StatusRejectedNode
                : roundTasks.Any(item => item.Status == ApprovalTaskStatus.Approved)
                    ? StatusApprovedNode
                    : StatusSkippedNode;
        progress.Tasks = roundTasks.Select(MapToTaskDto).ToList();
        return progress;
    }

    /// <summary>无实例节点行时的推导（升级前创建的实例）。</summary>
    private static List<ApprovalNodeProgressDto> BuildDerivedNodeProgress(
        IReadOnlyList<ApprovalFlowNode> nodes,
        IReadOnlyList<ApprovalTask> tasks,
        IReadOnlyList<ApprovalCc> ccs)
    {
        var result = new List<ApprovalNodeProgressDto>(nodes.Count);
        foreach (var node in nodes)
        {
            var progress = new ApprovalNodeProgressDto
            {
                NodeKey = node.NodeKey,
                Name = node.Name,
                NodeType = node.NodeType.ToString(),
                SortOrder = node.SortOrder
            };

            if (node.NodeType == ApprovalNodeType.Cc)
            {
                var sent = ccs.Any(item => item.NodeKey == node.NodeKey);
                progress.Activated = sent;
                progress.Status = sent ? StatusApprovedNode : StatusNotStarted;
                progress.State = (sent ? ApprovalNodeState.Completed : ApprovalNodeState.Pending).ToString();
                result.Add(progress);
                continue;
            }

            var nodeTasks = tasks.Where(item => item.NodeKey == node.NodeKey).ToList();
            if (nodeTasks.Count == 0)
            {
                progress.Status = StatusNotStarted;
                progress.State = ApprovalNodeState.Pending.ToString();
                result.Add(progress);
                continue;
            }

            var round = nodeTasks.Max(item => item.Round);
            var roundTasks = nodeTasks
                .Where(item => item.Round == round)
                .OrderBy(item => item.SortOrder)
                .ToList();
            progress.Activated = true;
            progress.Round = round;
            if (roundTasks.Any(item => item.Status == ApprovalTaskStatus.Pending))
            {
                progress.Status = StatusActive;
                progress.State = ApprovalNodeState.Active.ToString();
            }
            else if (roundTasks.Any(item => item.Status == ApprovalTaskStatus.Rejected))
            {
                progress.Status = StatusRejectedNode;
                progress.State = ApprovalNodeState.Active.ToString();
            }
            else if (roundTasks.Any(item => item.Status == ApprovalTaskStatus.Approved))
            {
                progress.Status = StatusApprovedNode;
                progress.State = ApprovalNodeState.Completed.ToString();
            }
            else
            {
                progress.Status = StatusSkippedNode;
                progress.State = ApprovalNodeState.Skipped.ToString();
            }

            progress.Tasks = roundTasks.Select(MapToTaskDto).ToList();
            result.Add(progress);
        }

        return result;
    }

    /// <summary>
    /// 任务是否可操作：所在节点仍处于活动状态。实例存在节点行时以节点状态为准（并行分支下当前节点
    /// 与处理节点可能不同）；无节点行的历史实例退回按实例当前节点判断。
    /// </summary>
    private static bool IsTaskActionable(
        ApprovalInstance instance,
        ApprovalTask task,
        IReadOnlyDictionary<string, ApprovalNodeState>? states)
    {
        if (task.Status != ApprovalTaskStatus.Pending || instance.Status != ApprovalInstanceStatus.Running)
        {
            return false;
        }

        if (states is not null && states.TryGetValue(task.NodeKey, out var state))
        {
            return state == ApprovalNodeState.Active;
        }

        return string.Equals(instance.CurrentNodeKey, task.NodeKey, StringComparison.Ordinal);
    }

    private static ApprovalTaskPermissionsDto? BuildPermissions(
        ApprovalInstance instance,
        ApprovalFlowNode node,
        ApprovalTask task,
        Guid viewerUserId,
        bool actionable,
        bool hasPreviousApproved)
    {
        if (!actionable || task.AssigneeUserId != viewerUserId)
        {
            return null;
        }

        return new ApprovalTaskPermissionsDto
        {
            CanApprove = true,
            CanTransfer = node.AllowTransfer,
            CanAddSign = node.AllowAddSign,
            CanReturnToPrevious = hasPreviousApproved,
            CanReturnToInitiator = node.AllowReturnToInitiator
        };
    }

    /// <summary>节点之前是否存在已通过的审批节点（决定是否展示“退回上一步”）。</summary>
    private static bool HasPreviousApprovedNode(string nodeKey, IReadOnlyList<ApprovalTask> instanceTasks) =>
        instanceTasks
            .Where(item => !string.Equals(item.NodeKey, nodeKey, StringComparison.Ordinal))
            .GroupBy(item => item.NodeKey)
            .Any(group => group.Any(item =>
                item.Round == group.Max(entry => entry.Round) && item.Status == ApprovalTaskStatus.Approved));

    private static ApprovalInstanceStatus ParseStatus(string status)
    {
        if (Enum.TryParse<ApprovalInstanceStatus>(status.Trim(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new BusinessException(
            ApprovalErrorCodes.InvalidRequest,
            $"不支持的实例状态过滤值 [{status}]。");
    }

    internal static ApprovalInstanceListDto MapToInstanceList(ApprovalInstance instance) => new()
    {
        Id = instance.Id,
        Number = instance.Number,
        DefinitionCode = instance.DefinitionCode,
        DefinitionName = instance.DefinitionName,
        DefinitionVersion = instance.DefinitionVersion,
        BusinessType = instance.BusinessType,
        BusinessId = instance.BusinessId,
        Title = instance.Title,
        InitiatorUserId = instance.InitiatorUserId,
        Status = instance.Status.ToString(),
        CurrentNodeKey = instance.CurrentNodeKey,
        CurrentNodeName = instance.CurrentNodeName,
        CreationTime = instance.CreationTime,
        CreatorId = instance.CreatorId,
        LastModificationTime = instance.LastModificationTime,
        LastModifierId = instance.LastModifierId,
        CompletedTime = instance.CompletedTime
    };

    private static ApprovalTaskDto MapToTaskDto(ApprovalTask task) => new()
    {
        Id = task.Id,
        InstanceId = task.InstanceId,
        NodeKey = task.NodeKey,
        NodeName = task.NodeName,
        NodeSort = task.NodeSort,
        TaskType = task.TaskType.ToString(),
        AssigneeUserId = task.AssigneeUserId,
        Status = task.Status.ToString(),
        SortOrder = task.SortOrder,
        Comment = task.Comment,
        DueAt = task.DueAt,
        HandledTime = task.HandledTime,
        CreationTime = task.CreationTime
    };

    private static ApprovalRecordDto MapToRecordDto(ApprovalRecord record) => new()
    {
        Id = record.Id,
        InstanceId = record.InstanceId,
        NodeKey = record.NodeKey,
        NodeName = record.NodeName,
        FromNodeKey = record.FromNodeKey,
        ToNodeKey = record.ToNodeKey,
        Action = record.Action.ToString(),
        OperatorUserId = record.OperatorUserId,
        TargetUserId = record.TargetUserId,
        Comment = record.Comment,
        CreationTime = record.CreationTime
    };
}
