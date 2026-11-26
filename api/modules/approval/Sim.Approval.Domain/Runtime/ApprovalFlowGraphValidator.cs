using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Approval.Domain.Runtime;

/// <summary>
/// 流程定义图结构校验 —— 发布前必须通过，保证运行时不会出现“永远等不到分支”或“推进死循环”。
/// 每一条问题都返回可读中文（后端权威判定，前端即时提示复用同一套规则）。
/// </summary>
public static class ApprovalFlowGraphValidator
{
    /// <summary>校验图结构；返回问题清单（空表示合法）。</summary>
    public static IReadOnlyList<string> Validate(ApprovalFlowGraph graph)
    {
        var problems = new List<string>();

        ValidateBoundaries(graph, problems);
        if (problems.Count > 0)
        {
            // 边界不合法时可达性与环检测没有意义，直接返回，避免重复噪声。
            return problems;
        }

        ValidateReachability(graph, problems);
        ValidateOutgoing(graph, problems);
        ValidateBranchEdges(graph, problems);
        ValidateCycles(graph, problems);

        if (!graph.Nodes.Any(item => item.NodeType == ApprovalNodeType.Approval))
        {
            problems.Add("流程至少需要一个审批节点。");
        }

        return problems;
    }

    /// <summary>校验并在不通过时抛出业务异常。</summary>
    public static void EnsureValid(ApprovalFlowGraph graph)
    {
        var problems = Validate(graph);
        if (problems.Count > 0)
        {
            throw new BusinessException(
                ApprovalErrorCodes.DefinitionInvalid,
                string.Join(" ", problems));
        }
    }

    private static void ValidateBoundaries(ApprovalFlowGraph graph, List<string> problems)
    {
        var startCount = graph.Nodes.Count(item => item.NodeType == ApprovalNodeType.Start);
        if (startCount != 1)
        {
            problems.Add($"流程必须恰好有一个开始节点，当前有 {startCount} 个。");
        }

        if (!graph.Nodes.Any(item => item.NodeType == ApprovalNodeType.End))
        {
            problems.Add("流程至少需要一个结束节点。");
        }
    }

    /// <summary>所有节点必须从开始节点可达（无孤立节点）。</summary>
    private static void ValidateReachability(ApprovalFlowGraph graph, List<string> problems)
    {
        if (graph.StartNode is null)
        {
            // 开始节点缺失已由 ValidateBoundaries 汇报，此处不再重复噪声。
            return;
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(graph.StartNode.NodeKey);
        visited.Add(graph.StartNode.NodeKey);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in graph.GetOutgoing(current))
            {
                if (visited.Add(edge.ToNodeKey))
                {
                    queue.Enqueue(edge.ToNodeKey);
                }
            }
        }

        var unreachable = graph.Nodes
            .Where(item => !visited.Contains(item.NodeKey))
            .Select(item => item.Name)
            .ToList();
        if (unreachable.Count > 0)
        {
            problems.Add($"存在从开始节点不可达的节点：{string.Join("、", unreachable)}。");
        }
    }

    /// <summary>
    /// 出边规则：开始节点恰好一条，结束节点不能有出线，抄送节点最多一条；
    /// 审批节点与网关支持多条出线（条件路由）；并行 fork 只由并行网关表达。
    /// </summary>
    private static void ValidateOutgoing(ApprovalFlowGraph graph, List<string> problems)
    {
        foreach (var node in graph.Nodes)
        {
            var outgoing = graph.GetOutgoing(node.NodeKey);

            if (node.NodeType == ApprovalNodeType.Start)
            {
                if (outgoing.Count != 1)
                {
                    problems.Add($"开始节点 [{node.Name}] 必须恰好有一条出线，当前有 {outgoing.Count} 条。");
                }

                continue;
            }

            if (outgoing.Count == 0)
            {
                if (node.NodeType != ApprovalNodeType.End)
                {
                    problems.Add($"节点 [{node.Name}] 没有出线，流程会在此中断。");
                }

                continue;
            }

            if (outgoing.Count > MaxOutgoingEdges(node.NodeType))
            {
                problems.Add(node.NodeType == ApprovalNodeType.End
                    ? $"结束节点 [{node.Name}] 不能有出线。"
                    : $"节点 [{node.Name}] 不支持多条出线，当前有 {outgoing.Count} 条。");
            }
        }
    }

    /// <summary>
    /// 节点出边数量上限：结束节点 0 条，开始 / 抄送节点 1 条；
    /// 审批节点与网关用于条件分支 / fork，允许多条。
    /// </summary>
    private static int MaxOutgoingEdges(ApprovalNodeType nodeType) => nodeType switch
    {
        ApprovalNodeType.End => 0,
        ApprovalNodeType.Start or ApprovalNodeType.Cc => 1,
        _ => int.MaxValue
    };

    /// <summary>
    /// 多出边规则：并行网关出线不得带条件；其余多出边节点（审批节点、排他网关）
    /// 必须保留且只能保留一条无条件的默认出边，避免运行期“所有条件均不成立”中断或分支不可达。
    /// </summary>
    private static void ValidateBranchEdges(ApprovalFlowGraph graph, List<string> problems)
    {
        foreach (var node in graph.Nodes)
        {
            var outgoing = graph.GetOutgoing(node.NodeKey);
            if (outgoing.Count <= 1)
            {
                continue;
            }

            if (node.NodeType == ApprovalNodeType.ParallelGateway)
            {
                if (outgoing.Any(edge => !string.IsNullOrWhiteSpace(edge.ConditionJson)))
                {
                    problems.Add($"并行网关 [{node.Name}] 的出线不能携带条件。");
                }

                continue;
            }

            var defaultEdges = outgoing.Count(edge => string.IsNullOrWhiteSpace(edge.ConditionJson));
            if (defaultEdges != 1)
            {
                problems.Add(
                    $"节点 [{node.Name}] 有多条出线时必须保留且只能保留一条无条件的默认出边，当前有 {defaultEdges} 条。");
            }
        }
    }

    /// <summary>流程定义不允许存在任何环；驳回重审使用“退回上一步”动作，不依赖图环。</summary>
    private static void ValidateCycles(ApprovalFlowGraph graph, List<string> problems)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            if (state.GetValueOrDefault(node.NodeKey) != 0)
            {
                continue;
            }

            if (HasCycle(node.NodeKey, graph, state))
            {
                problems.Add($"存在环路（起点节点 [{node.Name}]），审批流程不允许环；驳回重审请使用退回动作。");
                return;
            }
        }
    }

    private static bool HasCycle(
        string nodeKey,
        ApprovalFlowGraph graph,
        Dictionary<string, int> state)
    {
        if (state.TryGetValue(nodeKey, out var current))
        {
            return current == 1;
        }

        state[nodeKey] = 1;
        foreach (var edge in graph.GetOutgoing(nodeKey))
        {
            if (HasCycle(edge.ToNodeKey, graph, state))
            {
                return true;
            }
        }

        state[nodeKey] = 2;
        return false;
    }
}
