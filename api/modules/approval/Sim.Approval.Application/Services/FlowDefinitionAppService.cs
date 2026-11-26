using Microsoft.AspNetCore.Authorization;
using Sim.Approval.Application.Contracts;
using Sim.Approval.Application.Contracts.Dtos;
using Sim.Approval.Application.Contracts.Ports;
using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.IRepositories;
using Sim.Approval.Domain.Runtime;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.Framework.Domain.Shared.Exceptions;
using SqlSugar;

namespace Sim.Approval.Application.Services;

/// <summary>
/// 审批流程定义管理 —— 草稿创建与编辑、发布、停用、版本派生与草稿删除。
/// 已发布版本不可修改，需基于来源版本创建新草稿。
/// </summary>
[Authorize]
[Permission(ApprovalPermissions.ManageDefinition)]
public class FlowDefinitionAppService(
    IApprovalFlowDefinitionRepository definitionRepository,
    IApprovalFlowNodeRepository nodeRepository,
    IApprovalFlowEdgeRepository edgeRepository,
    IApprovalInstanceRepository instanceRepository,
    ApprovalBusinessRegistry businessRegistry) : ApplicationService, IFlowDefinitionAppService
{
    /// <summary>全部流程定义（按编码、版本倒序）。</summary>
    public async Task<List<ApprovalFlowDefinitionListDto>> GetListAsync()
    {
        var definitions = await definitionRepository.GetAll();
        if (definitions.Count == 0)
        {
            return [];
        }

        var nodeCounts = (await nodeRepository.GetAll())
            .GroupBy(item => item.DefinitionId)
            .ToDictionary(group => group.Key, group => group.Count());

        return definitions
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ThenByDescending(item => item.Version)
            .Select(item => MapToListItem(item, nodeCounts.GetValueOrDefault(item.Id)))
            .ToList();
    }

    /// <summary>流程定义明细（含节点与连线）。</summary>
    public async Task<ApprovalFlowDefinitionDto> GetAsync(Guid id)
    {
        var definition = await LoadRequiredAsync(id);
        var nodes = await LoadNodesAsync(definition.Id);
        var edges = await LoadEdgesAsync(definition.Id);
        return MapToDetail(definition, nodes, edges);
    }

    /// <summary>创建流程定义（草稿）；同编码已存在版本时自动派生下一个版本号。</summary>
    [SqlSugarUnitOfWork]
    public async Task<ApprovalFlowDefinitionDto> CreateAsync(CreateApprovalFlowDefinitionDto input)
    {
        var code = NormalizeCode(input.Code);
        var versions = await definitionRepository.GetListAsync(item => item.Code == code);
        var version = versions.Count == 0 ? 1 : versions.Max(item => item.Version) + 1;

        var definition = new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(),
            TenantScope = ApprovalFlowDefinition.CurrentTenantScope(),
            Code = code,
            Name = NormalizeName(input.Name),
            Description = NormalizeText(input.Description, 1024),
            Version = version,
            Status = ApprovalFlowStatus.Draft,
            BusinessType = NormalizeText(input.BusinessType, ApprovalConsts.MaxBusinessTypeLength),
            AllowWithdraw = input.AllowWithdraw,
            WithdrawWindowHours = input.WithdrawWindowHours,
            AutoPassWhenInitiator = input.AutoPassWhenInitiator,
            FallbackApproverUserIds = ApprovalRuntimeJson.SerializeUserIds(input.FallbackApproverUserIds),
            LayoutJson = NormalizeLayout(input.LayoutJson)
        };

        await InsertDefinitionAsync(definition);
        await InsertNodesAsync(definition, input.Nodes);
        await InsertEdgesAsync(definition, input.Edges);
        return await GetAsync(definition.Id);
    }

    /// <summary>更新草稿流程定义（节点与连线整体覆盖）。</summary>
    [SqlSugarUnitOfWork]
    public async Task<ApprovalFlowDefinitionDto> UpdateAsync(Guid id, UpdateApprovalFlowDefinitionDto input)
    {
        var definition = await LoadRequiredAsync(id);
        EnsureDraft(definition, "修改");

        definition.Name = NormalizeName(input.Name);
        definition.Description = NormalizeText(input.Description, 1024);
        definition.BusinessType = NormalizeText(input.BusinessType, ApprovalConsts.MaxBusinessTypeLength);
        definition.AllowWithdraw = input.AllowWithdraw;
        definition.WithdrawWindowHours = input.WithdrawWindowHours;
        definition.AutoPassWhenInitiator = input.AutoPassWhenInitiator;
        definition.FallbackApproverUserIds = ApprovalRuntimeJson.SerializeUserIds(input.FallbackApproverUserIds);
        definition.LayoutJson = NormalizeLayout(input.LayoutJson);
        await definitionRepository.UpdateAsync(definition);

        await edgeRepository.DeleteAsync(item => item.DefinitionId == definition.Id);
        await nodeRepository.DeleteAsync(item => item.DefinitionId == definition.Id);
        await InsertNodesAsync(definition, input.Nodes);
        await InsertEdgesAsync(definition, input.Edges);
        return await GetAsync(definition.Id);
    }

    /// <summary>发布前校验（不改变状态）：返回逐条问题，供设计器在保存后展示权威判定。</summary>
    public async Task<ApprovalFlowValidationResultDto> ValidateAsync(Guid id)
    {
        var definition = await LoadRequiredAsync(id);
        var nodes = await LoadNodesAsync(definition.Id);
        var edges = await LoadEdgesAsync(definition.Id);

        var problems = new List<string>();
        if (nodes.Count == 0)
        {
            problems.Add("流程至少需要一个审批节点。");
        }
        else
        {
            problems.AddRange(ApprovalFlowGraphValidator.Validate(ApprovalFlowGraph.Build(definition.Id, nodes, edges)));
        }

        problems.AddRange(CollectBusinessProblems(definition, nodes, edges));

        return new ApprovalFlowValidationResultDto
        {
            Valid = problems.Count == 0,
            Problems = problems
        };
    }

    /// <summary>发布流程定义（发布后不可修改）；发布前按图结构规则校验。</summary>
    [SqlSugarUnitOfWork]
    public async Task<ApprovalFlowDefinitionDto> PublishAsync(Guid id)
    {
        var definition = await LoadRequiredAsync(id);
        EnsureDraft(definition, "发布");

        var nodes = await LoadNodesAsync(definition.Id);
        if (!nodes.Any(item => item.NodeType == ApprovalNodeType.Approval))
        {
            throw new BusinessException(
                ApprovalErrorCodes.DefinitionNoApprovalNode,
                $"流程 [{definition.Name}] 缺少审批节点，不能发布。");
        }

        var edges = await LoadEdgesAsync(definition.Id);
        ApprovalFlowGraphValidator.EnsureValid(ApprovalFlowGraph.Build(definition.Id, nodes, edges));

        var businessProblems = CollectBusinessProblems(definition, nodes, edges);
        if (businessProblems.Count > 0)
        {
            throw new BusinessException(
                ApprovalErrorCodes.DefinitionInvalid,
                string.Join(" ", businessProblems));
        }

        // 同一编码的新版本发布后，旧版本不再作为“当前生效版本”，避免监控列表出现多个已发布版本。
        var previousVersions = await definitionRepository.GetListAsync(item =>
            item.TenantScope == definition.TenantScope &&
            item.Code == definition.Code &&
            item.Id != definition.Id &&
            item.Status == ApprovalFlowStatus.Published);
        if (previousVersions.Count > 0)
        {
            foreach (var previous in previousVersions)
            {
                previous.Status = ApprovalFlowStatus.Disabled;
            }

            await definitionRepository.UpdateManyAsync(previousVersions);
        }

        definition.Status = ApprovalFlowStatus.Published;
        definition.PublishedTime = DateTime.Now;
        await definitionRepository.UpdateAsync(definition);
        return await GetAsync(definition.Id);
    }

    /// <summary>发布与校验共用的业务规则检查：业务类型注册、条件 JSON 与字段类型。</summary>
    private List<string> CollectBusinessProblems(
        ApprovalFlowDefinition definition,
        IReadOnlyList<ApprovalFlowNode> nodes,
        IReadOnlyList<ApprovalFlowEdge> edges)
    {
        var problems = new List<string>();
        var descriptor = businessRegistry.FindDescriptor(definition.BusinessType ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(definition.BusinessType) && descriptor is null)
        {
            problems.Add($"业务类型 [{definition.BusinessType}] 未注册审批描述符。");
            return problems;
        }

        foreach (var node in nodes)
        {
            try
            {
                ApprovalConditionEvaluator.Validate(node.ConditionJson);
                ValidateDeclaredConditionFields(descriptor, node.ConditionJson);
            }
            catch (BusinessException exception)
            {
                problems.Add($"节点 [{node.Name}]：{exception.Message}");
            }
        }

        foreach (var edge in edges)
        {
            try
            {
                ApprovalConditionEvaluator.Validate(edge.ConditionJson);
                ValidateDeclaredConditionFields(descriptor, edge.ConditionJson);
            }
            catch (BusinessException exception)
            {
                problems.Add($"连线 [{edge.FromNodeKey} → {edge.ToNodeKey}]：{exception.Message}");
            }
        }

        return problems;
    }

    /// <summary>停用已发布流程定义（存在进行中实例时不允许）。</summary>
    [SqlSugarUnitOfWork]
    public async Task<ApprovalFlowDefinitionDto> DisableAsync(Guid id)
    {
        var definition = await LoadRequiredAsync(id);
        if (definition.Status != ApprovalFlowStatus.Published)
        {
            throw new BusinessException(
                ApprovalErrorCodes.InvalidRequest,
                $"流程 [{definition.Name}] 当前状态为 {definition.Status}，只有已发布的版本可以停用。");
        }

        var runningCount = await instanceRepository.GetQueryable()
            .Where(item => item.DefinitionId == definition.Id && item.Status == ApprovalInstanceStatus.Running)
            .CountAsync();
        if (runningCount > 0)
        {
            throw new BusinessException(
                ApprovalErrorCodes.DefinitionInUse,
                $"流程 [{definition.Name}] 存在 {runningCount} 个进行中的实例，不能停用。");
        }

        definition.Status = ApprovalFlowStatus.Disabled;
        await definitionRepository.UpdateAsync(definition);
        return await GetAsync(definition.Id);
    }

    /// <summary>基于指定版本创建新草稿版本（含节点与连线拷贝）。</summary>
    [SqlSugarUnitOfWork]
    public async Task<ApprovalFlowDefinitionDto> CreateVersionAsync(Guid id)
    {
        var source = await LoadRequiredAsync(id);
        var versions = await definitionRepository.GetListAsync(item => item.Code == source.Code);

        var draft = new ApprovalFlowDefinition
        {
            Id = Guid.NewGuid(),
            TenantScope = ApprovalFlowDefinition.CurrentTenantScope(),
            Code = source.Code,
            Name = source.Name,
            Description = source.Description,
            Version = versions.Max(item => item.Version) + 1,
            Status = ApprovalFlowStatus.Draft,
            BusinessType = source.BusinessType,
            AllowWithdraw = source.AllowWithdraw,
            WithdrawWindowHours = source.WithdrawWindowHours,
            AutoPassWhenInitiator = source.AutoPassWhenInitiator,
            FallbackApproverUserIds = source.FallbackApproverUserIds,
            LayoutJson = source.LayoutJson,
            BasedOnDefinitionId = source.Id
        };
        await InsertDefinitionAsync(draft);

        var sourceNodes = await LoadNodesAsync(source.Id);
        if (sourceNodes.Count > 0)
        {
            await nodeRepository.InsertManyAsync(sourceNodes.Select(item => new ApprovalFlowNode
            {
                Id = Guid.NewGuid(),
                DefinitionId = draft.Id,
                NodeKey = item.NodeKey,
                Name = item.Name,
                NodeType = item.NodeType,
                SortOrder = item.SortOrder,
                ApproverType = item.ApproverType,
                ApproverUserIds = item.ApproverUserIds,
                ApproverRoleName = item.ApproverRoleName,
                MultiApproveMode = item.MultiApproveMode,
                ConditionJson = item.ConditionJson,
                AllowTransfer = item.AllowTransfer,
                AllowAddSign = item.AllowAddSign,
                AllowReturnToInitiator = item.AllowReturnToInitiator,
                TimeoutHours = item.TimeoutHours,
                ApproverOrganizationId = item.ApproverOrganizationId,
                CcConfigJson = item.CcConfigJson,
                JoinPolicy = item.JoinPolicy
            }).ToList());
        }

        var sourceEdges = await LoadEdgesAsync(source.Id);
        if (sourceEdges.Count > 0)
        {
            await edgeRepository.InsertManyAsync(sourceEdges.Select(item => new ApprovalFlowEdge
            {
                Id = Guid.NewGuid(),
                DefinitionId = draft.Id,
                FromNodeKey = item.FromNodeKey,
                ToNodeKey = item.ToNodeKey,
                ConditionJson = item.ConditionJson,
                Label = item.Label,
                SortOrder = item.SortOrder
            }).ToList());
        }

        return await GetAsync(draft.Id);
    }

    /// <summary>删除草稿流程定义（连同节点与连线）。</summary>
    [SqlSugarUnitOfWork]
    public async Task DeleteAsync(Guid id)
    {
        var definition = await LoadRequiredAsync(id);
        EnsureDraft(definition, "删除");

        await edgeRepository.DeleteAsync(item => item.DefinitionId == definition.Id);
        await nodeRepository.DeleteAsync(item => item.DefinitionId == definition.Id);
        await definitionRepository.DeleteAsync(definition.Id);
    }

    // ── 内部实现 ──────────────────────────────────────────────────────────

    private async Task<ApprovalFlowDefinition> LoadRequiredAsync(Guid id)
    {
        var definition = await definitionRepository.GetAsync(id);
        return definition
            ?? throw new BusinessException(ApprovalErrorCodes.DefinitionNotFound, $"审批流程定义 {id} 不存在。");
    }

    private async Task<List<ApprovalFlowNode>> LoadNodesAsync(Guid definitionId)
    {
        var nodes = await nodeRepository.GetListAsync(item => item.DefinitionId == definitionId);
        return nodes.OrderBy(item => item.SortOrder).ToList();
    }

    private async Task<List<ApprovalFlowEdge>> LoadEdgesAsync(Guid definitionId)
    {
        var edges = await edgeRepository.GetListAsync(item => item.DefinitionId == definitionId);
        return edges
            .OrderBy(item => item.FromNodeKey, StringComparer.Ordinal)
            .ThenBy(item => item.SortOrder)
            .ToList();
    }

    private async Task InsertNodesAsync(ApprovalFlowDefinition definition, IReadOnlyList<SaveApprovalFlowNodeDto> inputs)
    {
        var nodes = BuildNodes(definition, inputs);
        if (!nodes.Any(item => item.NodeType == ApprovalNodeType.Approval))
        {
            throw new BusinessException(
                ApprovalErrorCodes.DefinitionNoApprovalNode,
                "流程至少需要一个审批节点。");
        }

        await nodeRepository.InsertManyAsync(nodes);
    }

    /// <summary>
    /// 写入连线。连线为空表示保留线性语义（引擎按节点顺序合成开始/结束与串联连线），
    /// 因此不做结构强制；一旦给出连线，则要求引用已存在的节点。
    /// </summary>
    private async Task InsertEdgesAsync(ApprovalFlowDefinition definition, IReadOnlyList<SaveApprovalFlowEdgeDto> inputs)
    {
        if (inputs.Count == 0)
        {
            return;
        }

        var nodeKeys = (await LoadNodesAsync(definition.Id))
            .Select(item => item.NodeKey)
            .ToHashSet(StringComparer.Ordinal);
        var descriptor = businessRegistry.FindDescriptor(definition.BusinessType ?? string.Empty);
        var edges = new List<ApprovalFlowEdge>(inputs.Count);
        var seen = new HashSet<(string From, string To)>();
        var sortOrders = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var input in inputs)
        {
            var from = (input.FromNodeKey ?? string.Empty).Trim();
            var to = (input.ToNodeKey ?? string.Empty).Trim();
            if (!nodeKeys.Contains(from) || !nodeKeys.Contains(to))
            {
                throw new BusinessException(
                    ApprovalErrorCodes.InvalidRequest,
                    $"连线 [{from} → {to}] 引用了不存在的节点。");
            }

            if (from == to)
            {
                throw new BusinessException(ApprovalErrorCodes.InvalidRequest, $"连线 [{from}] 的起点与终点相同。");
            }

            if (!seen.Add((from, to)))
            {
                throw new BusinessException(ApprovalErrorCodes.InvalidRequest, $"连线 [{from} → {to}] 重复。");
            }

            ApprovalConditionEvaluator.Validate(input.ConditionJson);
            ValidateDeclaredConditionFields(descriptor, input.ConditionJson);

            var sortOrder = sortOrders.GetValueOrDefault(from) + 1;
            sortOrders[from] = sortOrder;
            edges.Add(new ApprovalFlowEdge
            {
                Id = Guid.NewGuid(),
                DefinitionId = definition.Id,
                FromNodeKey = from,
                ToNodeKey = to,
                ConditionJson = NormalizeText(input.ConditionJson, ApprovalConsts.MaxConditionJsonLength),
                Label = NormalizeText(input.Label, ApprovalConsts.MaxEdgeLabelLength),
                SortOrder = sortOrder
            });
        }

        await edgeRepository.InsertManyAsync(edges);
    }

    /// <summary>
    /// 条件只能引用业务类型已声明且可比较的字段（附件不可比较）。
    /// 业务类型未声明字段清单时放行：否则条件分支在任何业务类型下都无法保存。
    /// </summary>
    private static void ValidateDeclaredConditionFields(IApprovalBusinessDescriptor? descriptor, string? conditionJson)
    {
        if (descriptor is null || descriptor.Fields.Count == 0)
        {
            return;
        }

        var declared = descriptor.Fields
            .Where(field => field.Key.Length > 0 &&
                            ApprovalFieldSchema.ParseFieldType(field.Type).IsConditionComparable())
            .Select(field => field.Key.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var unknown = ApprovalConditionEvaluator.ReferencedFields(conditionJson)
            .Where(field => !declared.Contains(field))
            .ToList();
        if (unknown.Count > 0)
        {
            throw new BusinessException(
                ApprovalErrorCodes.ConditionInvalid,
                $"条件字段 [{string.Join(", ", unknown)}] 未在业务类型 [{descriptor.BusinessType}] 中声明。");
        }

        ApprovalFieldSchema.EnsureConditionOperatorsMatchFieldTypes(descriptor, conditionJson);
    }

    private List<ApprovalFlowNode> BuildNodes(
        ApprovalFlowDefinition definition,
        IReadOnlyList<SaveApprovalFlowNodeDto> inputs)
    {
        if (inputs.Count == 0)
        {
            throw new BusinessException(ApprovalErrorCodes.DefinitionNoApprovalNode, "流程至少需要一个审批节点。");
        }

        var descriptor = businessRegistry.FindDescriptor(definition.BusinessType ?? string.Empty);
        var nodes = new List<ApprovalFlowNode>(inputs.Count);
        var nodeKeys = new HashSet<string>(StringComparer.Ordinal);
        var sortOrder = 0;

        foreach (var input in inputs)
        {
            var nodeKey = (input.NodeKey ?? string.Empty).Trim();
            if (nodeKey.Length == 0)
            {
                throw new BusinessException(ApprovalErrorCodes.InvalidRequest, "节点标识不能为空。");
            }

            if (!nodeKeys.Add(nodeKey))
            {
                throw new BusinessException(
                    ApprovalErrorCodes.DefinitionNodeKeyDuplicated,
                    $"节点标识 [{nodeKey}] 在同一流程内重复。");
            }

            ApprovalConditionEvaluator.Validate(input.ConditionJson);
            ValidateDeclaredConditionFields(descriptor, input.ConditionJson);

            var nodeType = ParseEnum<ApprovalNodeType>(input.NodeType, "节点类型");
            var node = new ApprovalFlowNode
            {
                Id = Guid.NewGuid(),
                DefinitionId = definition.Id,
                NodeKey = nodeKey,
                Name = NormalizeName(input.Name),
                NodeType = nodeType,
                SortOrder = ++sortOrder,
                ApproverType = ParseEnum<ApprovalApproverType>(input.ApproverType, "审批人来源"),
                ApproverUserIds = ApprovalRuntimeJson.SerializeUserIds(input.ApproverUserIds),
                ApproverRoleName = NormalizeText(input.ApproverRoleName, 128),
                MultiApproveMode = ParseEnum<ApprovalMultiApproveMode>(input.MultiApproveMode, "多人审批模式"),
                ConditionJson = NormalizeText(input.ConditionJson, ApprovalConsts.MaxConditionJsonLength),
                AllowTransfer = input.AllowTransfer,
                AllowAddSign = input.AllowAddSign,
                AllowReturnToInitiator = input.AllowReturnToInitiator,
                TimeoutHours = input.TimeoutHours,
                ApproverOrganizationId = input.ApproverOrganizationId,
                CcConfigJson = NormalizeText(input.CcConfigJson, ApprovalConsts.MaxCcConfigJsonLength),
                JoinPolicy = ParseEnum<ApprovalJoinPolicy>(input.JoinPolicy, "汇聚策略")
            };

            ValidateNode(node);
            nodes.Add(node);
        }

        return nodes;
    }

    private static void ValidateNode(ApprovalFlowNode node)
    {
        if (node.NodeType == ApprovalNodeType.Cc)
        {
            var configuration = ApprovalRuntimeJson.DeserializeCcConfig(node.CcConfigJson);
            if (configuration.UserIds.Count == 0 && configuration.Roles.Count == 0 && !configuration.Initiator)
            {
                throw new BusinessException(
                    ApprovalErrorCodes.InvalidRequest,
                    $"抄送节点 [{node.Name}] 未配置抄送对象。");
            }

            return;
        }

        if (!node.NodeType.IsApproval())
        {
            // 开始/网关/结束节点不承载审批人配置。
            return;
        }

        switch (node.ApproverType)
        {
            case ApprovalApproverType.SpecifiedUsers
                when ApprovalRuntimeJson.DeserializeUserIds(node.ApproverUserIds).Count == 0:
                throw new BusinessException(
                    ApprovalErrorCodes.InvalidRequest,
                    $"审批节点 [{node.Name}] 未指定审批人。");
            case ApprovalApproverType.Role when string.IsNullOrWhiteSpace(node.ApproverRoleName):
                throw new BusinessException(
                    ApprovalErrorCodes.InvalidRequest,
                    $"审批节点 [{node.Name}] 未指定审批角色。");
            case ApprovalApproverType.OrganizationManager or ApprovalApproverType.OrganizationMembers
                when node.ApproverOrganizationId is null:
                throw new BusinessException(
                    ApprovalErrorCodes.InvalidRequest,
                    $"审批节点 [{node.Name}] 未选择审批组织。");
        }
    }

    private static void EnsureDraft(ApprovalFlowDefinition definition, string action)
    {
        if (definition.Status != ApprovalFlowStatus.Draft)
        {
            throw new BusinessException(
                ApprovalErrorCodes.DefinitionPublished,
                $"流程 [{definition.Name}] 当前状态为 {definition.Status}，只有草稿版本可以{action}。");
        }
    }

    private async Task InsertDefinitionAsync(ApprovalFlowDefinition definition)
    {
        try
        {
            await definitionRepository.InsertAsync(definition);
        }
        catch (SqlSugarException exception) when (
            exception.Message.Contains(
                "uidx_approval_flow_definition_scope_version",
                StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                ApprovalErrorCodes.DefinitionVersionDuplicated,
                $"流程 [{definition.Code}] 的版本 {definition.Version} 已由其他请求创建，请刷新后重试。");
        }
    }

    private static string NormalizeCode(string? code)
    {
        var value = (code ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            throw new BusinessException(ApprovalErrorCodes.InvalidRequest, "流程编码不能为空。");
        }

        if (value.Length > ApprovalConsts.MaxFlowCodeLength ||
            value.Any(character => !char.IsLetterOrDigit(character) && character is not ('-' or '_' or '.')))
        {
            throw new BusinessException(
                ApprovalErrorCodes.InvalidRequest,
                "流程编码仅支持字母、数字、中划线、下划线与点，且长度不超过 "
                + $"{ApprovalConsts.MaxFlowCodeLength}。");
        }

        return value;
    }

    private static string NormalizeName(string? name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            throw new BusinessException(ApprovalErrorCodes.InvalidRequest, "名称不能为空。");
        }

        return value;
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length > maxLength ? text[..maxLength] : text;
    }

    /// <summary>布局为空时返回 null（引擎与列表都不读该字段，仅设计器还原用）。</summary>
    private static string? NormalizeLayout(string? layoutJson) =>
        NormalizeText(layoutJson, ApprovalConsts.MaxLayoutJsonLength);

    private static TEnum ParseEnum<TEnum>(string? value, string fieldName)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new BusinessException(ApprovalErrorCodes.InvalidRequest, $"{fieldName} [{value}] 不合法。");
    }

    private static ApprovalFlowDefinitionListDto MapToListItem(ApprovalFlowDefinition definition, int nodeCount) => new()
    {
        Id = definition.Id,
        Code = definition.Code,
        Name = definition.Name,
        Description = definition.Description,
        Version = definition.Version,
        Status = definition.Status.ToString(),
        BusinessType = definition.BusinessType,
        NodeCount = nodeCount,
        PublishedTime = definition.PublishedTime,
        CreationTime = definition.CreationTime,
        CreatorId = definition.CreatorId,
        LastModificationTime = definition.LastModificationTime,
        LastModifierId = definition.LastModifierId
    };

    private static ApprovalFlowDefinitionDto MapToDetail(
        ApprovalFlowDefinition definition,
        IReadOnlyCollection<ApprovalFlowNode> nodes,
        IReadOnlyCollection<ApprovalFlowEdge> edges)
    {
        var detail = new ApprovalFlowDefinitionDto
        {
            Id = definition.Id,
            Code = definition.Code,
            Name = definition.Name,
            Description = definition.Description,
            Version = definition.Version,
            Status = definition.Status.ToString(),
            BusinessType = definition.BusinessType,
            NodeCount = nodes.Count,
            PublishedTime = definition.PublishedTime,
            CreationTime = definition.CreationTime,
            CreatorId = definition.CreatorId,
            LastModificationTime = definition.LastModificationTime,
            LastModifierId = definition.LastModifierId,
            AllowWithdraw = definition.AllowWithdraw,
            WithdrawWindowHours = definition.WithdrawWindowHours,
            AutoPassWhenInitiator = definition.AutoPassWhenInitiator,
            FallbackApproverUserIds = ApprovalRuntimeJson.DeserializeUserIds(definition.FallbackApproverUserIds).ToList(),
            BasedOnDefinitionId = definition.BasedOnDefinitionId,
            LayoutJson = definition.LayoutJson
        };

        detail.Nodes = nodes
            .OrderBy(item => item.SortOrder)
            .Select(item => new ApprovalFlowNodeDto
            {
                Id = item.Id,
                NodeKey = item.NodeKey,
                Name = item.Name,
                NodeType = item.NodeType.ToString(),
                SortOrder = item.SortOrder,
                ApproverType = item.ApproverType.ToString(),
                ApproverUserIds = ApprovalRuntimeJson.DeserializeUserIds(item.ApproverUserIds).ToList(),
                ApproverRoleName = item.ApproverRoleName,
                MultiApproveMode = item.MultiApproveMode.ToString(),
                ConditionJson = item.ConditionJson,
                AllowTransfer = item.AllowTransfer,
                AllowAddSign = item.AllowAddSign,
                AllowReturnToInitiator = item.AllowReturnToInitiator,
                TimeoutHours = item.TimeoutHours,
                ApproverOrganizationId = item.ApproverOrganizationId,
                CcConfigJson = item.CcConfigJson,
                JoinPolicy = item.JoinPolicy.ToString()
            })
            .ToList();

        detail.Edges = edges
            .OrderBy(item => item.FromNodeKey, StringComparer.Ordinal)
            .ThenBy(item => item.SortOrder)
            .Select(item => new ApprovalFlowEdgeDto
            {
                Id = item.Id,
                FromNodeKey = item.FromNodeKey,
                ToNodeKey = item.ToNodeKey,
                ConditionJson = item.ConditionJson,
                Label = item.Label,
                SortOrder = item.SortOrder
            })
            .ToList();

        return detail;
    }
}
