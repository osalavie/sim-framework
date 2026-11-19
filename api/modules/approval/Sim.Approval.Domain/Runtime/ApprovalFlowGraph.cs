using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Approval.Domain.Runtime;

/// <summary>
/// 审批流程有向图（运行时视图）。引擎只依赖本类型做拓扑与就绪判定，不直接遍历节点表。
/// <para>
/// 兼容第一阶段线性定义：当定义没有连线时，按 <c>SortOrder</c> **合成** <c>Start → n1 → … → nN → End</c>，
/// 使线性流程成为有向图的一个特例（任意时刻最多一个节点 Active，永远不会触发合流等待）。
/// </para>
/// </summary>
public sealed class ApprovalFlowGraph
{
    private readonly Dictionary<string, ApprovalFlowNode> _nodes;
    private readonly Dictionary<string, List<ApprovalFlowEdge>> _outgoing;
    private readonly Dictionary<string, List<ApprovalFlowEdge>> _incoming;

    private ApprovalFlowGraph(
        IReadOnlyList<ApprovalFlowNode> nodes,
        IReadOnlyList<ApprovalFlowEdge> edges,
        bool synthesizedBoundaries)
    {
        Nodes = nodes;
        Edges = edges;
        HasSynthesizedBoundaries = synthesizedBoundaries;
        _nodes = nodes
            .GroupBy(item => item.NodeKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var valid = edges
            .Where(item => _nodes.ContainsKey(item.FromNodeKey) && _nodes.ContainsKey(item.ToNodeKey))
            .ToList();
        _outgoing = valid
            .GroupBy(item => item.FromNodeKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.SortOrder).ToList(),
                StringComparer.Ordinal);
        _incoming = valid
            .GroupBy(item => item.ToNodeKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(item => item.SortOrder).ToList(),
                StringComparer.Ordinal);

        StartNode = nodes.FirstOrDefault(item => item.NodeType == ApprovalNodeType.Start);
        EndNode = nodes.FirstOrDefault(item => item.NodeType == ApprovalNodeType.End);
    }

    /// <summary>全部节点，按执行顺序排列（顺序只影响同一轮内的激活次序，就绪判定不依赖顺序）。</summary>
    public IReadOnlyList<ApprovalFlowNode> Nodes { get; }

    /// <summary>全部连线。</summary>
    public IReadOnlyList<ApprovalFlowEdge> Edges { get; }

    /// <summary>是否为线性定义合成的图（开始/结束节点仅为内存节点，不落库）。</summary>
    public bool HasSynthesizedBoundaries { get; }

    /// <summary>开始节点；定义缺少开始节点时为 <c>null</c>（由校验器汇报问题，构建不抛异常）。</summary>
    public ApprovalFlowNode? StartNode { get; }

    /// <summary>结束节点；定义缺少结束节点时为 <c>null</c>（由校验器汇报问题，构建不抛异常）。</summary>
    public ApprovalFlowNode? EndNode { get; }

    /// <summary>
    /// 构建图。定义只有节点没有连线（第一阶段线性定义）时合成开始与结束节点；
    /// 已含开始/结束节点的定义按连线拓扑构建。
    /// </summary>
    public static ApprovalFlowGraph Build(
        Guid definitionId,
        IReadOnlyList<ApprovalFlowNode> nodes,
        IReadOnlyList<ApprovalFlowEdge> edges)
    {
        if (nodes.Count == 0)
        {
            throw new BusinessException(ApprovalErrorCodes.DefinitionInvalid, "流程定义没有任何节点。");
        }

        if (edges.Count == 0 && !nodes.Any(item => item.NodeType is ApprovalNodeType.Start or ApprovalNodeType.End))
        {
            return BuildLinear(definitionId, nodes);
        }

        var ordered = nodes
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.NodeKey, StringComparer.Ordinal)
            .ToList();
        return new ApprovalFlowGraph(ordered, edges.ToList(), synthesizedBoundaries: false);
    }

    /// <summary>把线性节点串成 <c>Start → n1 → … → nN → End</c>。</summary>
    private static ApprovalFlowGraph BuildLinear(Guid definitionId, IReadOnlyList<ApprovalFlowNode> nodes)
    {
        var ordered = nodes
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.NodeKey, StringComparer.Ordinal)
            .ToList();

        var start = new ApprovalFlowNode
        {
            Id = Guid.Empty,
            DefinitionId = definitionId,
            NodeKey = ApprovalConsts.SynthesizedStartNodeKey,
            Name = "开始",
            NodeType = ApprovalNodeType.Start,
            SortOrder = 0
        };
        var end = new ApprovalFlowNode
        {
            Id = Guid.Empty,
            DefinitionId = definitionId,
            NodeKey = ApprovalConsts.SynthesizedEndNodeKey,
            Name = "结束",
            NodeType = ApprovalNodeType.End,
            SortOrder = ordered.Max(item => item.SortOrder) + 1,
            JoinPolicy = ApprovalJoinPolicy.AllArrival
        };

        var all = new List<ApprovalFlowNode>(ordered.Count + 2) { start };
        all.AddRange(ordered);
        all.Add(end);

        var edges = new List<ApprovalFlowEdge>(all.Count - 1);
        for (var index = 0; index < all.Count - 1; index++)
        {
            edges.Add(new ApprovalFlowEdge
            {
                Id = Guid.Empty,
                DefinitionId = definitionId,
                FromNodeKey = all[index].NodeKey,
                ToNodeKey = all[index + 1].NodeKey,
                SortOrder = 1
            });
        }

        return new ApprovalFlowGraph(all, edges, synthesizedBoundaries: true);
    }

    /// <summary>按标识查找节点。</summary>
    public ApprovalFlowNode? FindNode(string? nodeKey) =>
        string.IsNullOrWhiteSpace(nodeKey) ? null : _nodes.GetValueOrDefault(nodeKey);

    /// <summary>节点出边（按顺序）。</summary>
    public IReadOnlyList<ApprovalFlowEdge> GetOutgoing(string nodeKey) =>
        _outgoing.GetValueOrDefault(nodeKey) ?? [];

    /// <summary>节点入边（按顺序）。</summary>
    public IReadOnlyList<ApprovalFlowEdge> GetIncoming(string nodeKey) =>
        _incoming.GetValueOrDefault(nodeKey) ?? [];

    /// <summary>实例初始化所需的节点快照（按执行顺序）。</summary>
    public IEnumerable<(string NodeKey, string Name, ApprovalNodeType NodeType, int SortOrder)> Snapshot() =>
        Nodes.Select(item => (item.NodeKey, item.Name, item.NodeType, item.SortOrder));

    /// <summary>
    /// 节点完成后被选中的出边目标：排他网关与多出边节点按顺序取首个条件成立的出边
    /// （无条件出边即默认出边）；并行网关激活全部出边；结束节点无出边。
    /// </summary>
    public IReadOnlyList<string> ResolveSelectedTargets(
        ApprovalFlowNode node,
        IReadOnlyDictionary<string, string> context)
    {
        var outgoing = GetOutgoing(node.NodeKey);
        if (outgoing.Count == 0)
        {
            return [];
        }

        if (node.NodeType == ApprovalNodeType.ParallelGateway)
        {
            return outgoing.Select(item => item.ToNodeKey).Distinct(StringComparer.Ordinal).ToList();
        }

        foreach (var edge in outgoing)
        {
            if (ApprovalConditionEvaluator.IsActive(edge.ConditionJson, context))
            {
                return [edge.ToNodeKey];
            }
        }

        throw new BusinessException(
            ApprovalErrorCodes.DefinitionInvalid,
            $"节点 [{node.Name}] 的所有出边条件均不成立，且没有默认出边，流程无法继续。");
    }

    /// <summary>
    /// 节点就绪判定：节点为 <c>Pending</c> 且入边已给出来路。
    /// <c>AnyArrival</c> 任一路到达即可推进；<c>AllArrival</c> 等待全部入边到达或确定死亡（且至少一路到达）。
    /// </summary>
    public bool IsReady(ApprovalFlowNode node, IReadOnlyDictionary<string, ApprovalNodeState> states)
    {
        if (states.GetValueOrDefault(node.NodeKey, ApprovalNodeState.Pending) != ApprovalNodeState.Pending)
        {
            return false;
        }

        var incoming = GetIncoming(node.NodeKey);
        if (incoming.Count == 0)
        {
            return true;
        }

        var live = 0;
        var settled = 0;
        foreach (var edge in incoming)
        {
            var sourceState = states.GetValueOrDefault(edge.FromNodeKey, ApprovalNodeState.Pending);
            if (sourceState == ApprovalNodeState.Completed)
            {
                live++;
            }

            if (sourceState.IsSettled())
            {
                settled++;
            }
        }

        return ResolveJoinPolicy(node, incoming.Count) == ApprovalJoinPolicy.AllArrival
            ? live > 0 && settled == incoming.Count
            : live > 0;
    }

    /// <summary>
    /// 有效汇聚策略。<c>ParallelGateway</c> 的 join 与多入边的 <c>End</c> 恒为 <c>AllArrival</c>：
    /// 前者是并行等待语义，后者避免任一支路提前到达就把实例判为通过、把另一支路的在途任务静默跳过。
    /// 其余节点按定义配置，单入边节点两种策略等价。
    /// </summary>
    public static ApprovalJoinPolicy ResolveJoinPolicy(ApprovalFlowNode node, int incomingCount) =>
        incomingCount > 1 && node.NodeType is ApprovalNodeType.ParallelGateway or ApprovalNodeType.End
            ? ApprovalJoinPolicy.AllArrival
            : node.JoinPolicy;

    /// <summary>
    /// 死分支传播：入边来源全部被跳过的未到达节点同样置为跳过，
    /// 保证合流节点不会永久等待一条不可能到达的分支。返回本次新增的跳过节点。
    /// </summary>
    public IReadOnlyList<ApprovalFlowNode> ResolveDeadBranches(
        IReadOnlyDictionary<string, ApprovalNodeState> states)
    {
        var dead = new List<ApprovalFlowNode>();
        foreach (var node in Nodes)
        {
            if (states.GetValueOrDefault(node.NodeKey, ApprovalNodeState.Pending) != ApprovalNodeState.Pending)
            {
                continue;
            }

            var incoming = GetIncoming(node.NodeKey);
            if (incoming.Count == 0)
            {
                continue;
            }

            var allSkipped = incoming.All(edge =>
                states.GetValueOrDefault(edge.FromNodeKey, ApprovalNodeState.Pending) == ApprovalNodeState.Skipped);
            if (allSkipped)
            {
                dead.Add(node);
            }
        }

        return dead;
    }

    /// <summary>排他分支未选中的出边目标需要立即剪枝（其余分支不再等待）。</summary>
    public IReadOnlyList<ApprovalFlowNode> ResolvePrunedTargets(
        ApprovalFlowNode node,
        IReadOnlyDictionary<string, ApprovalNodeState> states,
        IReadOnlyDictionary<string, string> context)
    {
        var outgoing = GetOutgoing(node.NodeKey);
        if (outgoing.Count <= 1 || node.NodeType == ApprovalNodeType.ParallelGateway)
        {
            return [];
        }

        var selected = ResolveSelectedTargets(node, context).ToHashSet(StringComparer.Ordinal);
        var reachableFromSelected = ResolveReachableNodes(selected);
        var pruned = new List<ApprovalFlowNode>();
        foreach (var edge in outgoing)
        {
            if (selected.Contains(edge.ToNodeKey) ||
                reachableFromSelected.Contains(edge.ToNodeKey) ||
                states.GetValueOrDefault(edge.ToNodeKey, ApprovalNodeState.Pending) != ApprovalNodeState.Pending)
            {
                continue;
            }

            if (FindNode(edge.ToNodeKey) is { } target)
            {
                pruned.Add(target);
            }
        }

        return pruned;
    }

    /// <summary>
    /// 解析从已选目标继续可达的节点。排他网关的未选中出边如果直接指向已选路径后续的汇合节点，
    /// 该汇合节点仍然有效，不能被当成未选分支剪掉。
    /// </summary>
    private HashSet<string> ResolveReachableNodes(IEnumerable<string> startNodeKeys)
    {
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(startNodeKeys);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in GetOutgoing(current))
            {
                if (reachable.Add(edge.ToNodeKey))
                {
                    queue.Enqueue(edge.ToNodeKey);
                }
            }
        }

        return reachable;
    }
}
