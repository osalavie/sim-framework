using Sim.Approval.Domain.Entities;
using Sim.Approval.Domain.Runtime;
using Sim.Approval.Domain.Shared;
using Sim.Framework.Domain.Shared.Exceptions;

namespace Sim.Security.Tests;

/// <summary>
/// 有向图拓扑与发布前校验的纯单元测试（不依赖宿主与数据库）：
/// 守住第二阶段的分支选择、合流等待、死分支传播语义与发布前校验规则。
/// </summary>
public sealed class ApprovalFlowGraphTests
{
    private const string LargeAmountCondition =
        """{ "match": "all", "rules": [ { "field": "amount", "operator": "greaterOrEqual", "value": "1000" } ] }""";

    [Fact]
    public void Linear_definition_should_synthesize_start_and_end_boundaries()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("n1", ApprovalNodeType.Approval, 1), Node("n2", ApprovalNodeType.Approval, 2)],
            []);

        Assert.True(graph.HasSynthesizedBoundaries);
        Assert.Equal(ApprovalConsts.SynthesizedStartNodeKey, graph.StartNode!.NodeKey);
        Assert.Equal(ApprovalConsts.SynthesizedEndNodeKey, graph.EndNode!.NodeKey);
        Assert.Equal(4, graph.Nodes.Count);
        Assert.Equal(3, graph.Edges.Count);
        Assert.Equal(
            $"{ApprovalConsts.SynthesizedStartNodeKey},n1,n2,{ApprovalConsts.SynthesizedEndNodeKey}",
            string.Join(",", graph.Nodes.Select(item => item.NodeKey)));
        Assert.Empty(Validate(graph.Nodes, graph.Edges));
    }

    [Fact]
    public void Graph_without_boundaries_should_build_and_be_reported_instead_of_throwing()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("n1", ApprovalNodeType.Approval, 1)],
            [Edge("n1", "n2")]);

        // 缺少开始/结束节点时构建不抛异常，由校验器给出可读问题（可达性校验自动跳过）。
        Assert.Null(graph.StartNode);
        Assert.Null(graph.EndNode);
        var problems = ApprovalFlowGraphValidator.Validate(graph);
        Assert.Contains(problems, item => item.Contains("开始节点", StringComparison.Ordinal));
        Assert.Contains(problems, item => item.Contains("结束节点", StringComparison.Ordinal));
    }

    [Fact]
    public void Node_should_not_be_ready_until_an_incoming_source_completed()
    {
        var graph = BuildSplitJoinGraph(ApprovalJoinPolicy.AnyArrival);
        var join = graph.FindNode("join")!;

        Assert.False(graph.IsReady(join, States(("a", ApprovalNodeState.Pending), ("b", ApprovalNodeState.Pending))));
        Assert.False(graph.IsReady(join, States(("a", ApprovalNodeState.Active), ("b", ApprovalNodeState.Active))));
        Assert.True(graph.IsReady(join, States(("a", ApprovalNodeState.Completed), ("b", ApprovalNodeState.Pending))));
        Assert.True(graph.IsReady(join, States(("a", ApprovalNodeState.Completed), ("b", ApprovalNodeState.Skipped))));

        // 已激活或已完成的节点不会再次就绪。
        Assert.False(graph.IsReady(join, States(("a", ApprovalNodeState.Completed), ("join", ApprovalNodeState.Active))));
    }

    [Fact]
    public void All_arrival_join_should_wait_for_every_incoming_edge_to_settle()
    {
        var graph = BuildSplitJoinGraph(ApprovalJoinPolicy.AllArrival);
        var join = graph.FindNode("join")!;

        Assert.False(graph.IsReady(join, States(("a", ApprovalNodeState.Completed), ("b", ApprovalNodeState.Pending))));
        Assert.False(graph.IsReady(join, States(("a", ApprovalNodeState.Completed), ("b", ApprovalNodeState.Active))));
        Assert.True(graph.IsReady(join, States(("a", ApprovalNodeState.Completed), ("b", ApprovalNodeState.Completed))));
        // 分支确定死亡时不再等待：至少一路到达 + 其余已定案。
        Assert.True(graph.IsReady(join, States(("a", ApprovalNodeState.Completed), ("b", ApprovalNodeState.Skipped))));
        // 全部死亡则无人到达，不应推进。
        Assert.False(graph.IsReady(join, States(("a", ApprovalNodeState.Skipped), ("b", ApprovalNodeState.Skipped))));
    }

    [Theory]
    [InlineData(ApprovalNodeType.ParallelGateway)]
    [InlineData(ApprovalNodeType.End)]
    public void Multi_incoming_join_nodes_should_force_all_arrival(ApprovalNodeType nodeType)
    {
        var node = Node("join", nodeType, 2, ApprovalJoinPolicy.AnyArrival);
        Assert.Equal(ApprovalJoinPolicy.AllArrival, ApprovalFlowGraph.ResolveJoinPolicy(node, 2));
        // 单入边节点两种策略等价，不强制改写。
        Assert.Equal(ApprovalJoinPolicy.AnyArrival, ApprovalFlowGraph.ResolveJoinPolicy(node, 1));
    }

    [Fact]
    public void Approval_node_join_policy_should_follow_definition()
    {
        Assert.Equal(
            ApprovalJoinPolicy.AnyArrival,
            ApprovalFlowGraph.ResolveJoinPolicy(Node("a", ApprovalNodeType.Approval, 1), 2));
        Assert.Equal(
            ApprovalJoinPolicy.AllArrival,
            ApprovalFlowGraph.ResolveJoinPolicy(
                Node("a", ApprovalNodeType.Approval, 1, ApprovalJoinPolicy.AllArrival),
                2));
        // 排他网关按「任一路到达」推进，不会被强制为合流等待。
        Assert.Equal(
            ApprovalJoinPolicy.AnyArrival,
            ApprovalFlowGraph.ResolveJoinPolicy(Node("gw", ApprovalNodeType.ExclusiveGateway, 1), 2));
    }

    [Fact]
    public void Pending_node_should_die_when_all_its_sources_died()
    {
        var graph = BuildSplitJoinGraph(ApprovalJoinPolicy.AllArrival);

        var dead = graph.ResolveDeadBranches(
            States(("a", ApprovalNodeState.Skipped), ("b", ApprovalNodeState.Skipped)));
        Assert.Equal("join", string.Join(",", dead.Select(item => item.NodeKey)));

        Assert.Empty(graph.ResolveDeadBranches(
            States(("a", ApprovalNodeState.Skipped), ("b", ApprovalNodeState.Completed))));
        Assert.Empty(graph.ResolveDeadBranches(
            States(("a", ApprovalNodeState.Skipped), ("b", ApprovalNodeState.Pending))));
    }

    [Fact]
    public void Exclusive_gateway_should_select_first_matching_edge_and_prune_the_others()
    {
        var gateway = Node("gw", ApprovalNodeType.ExclusiveGateway, 1);
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), gateway, Node("big", ApprovalNodeType.Approval, 2),
                Node("small", ApprovalNodeType.Approval, 2)],
            [Edge("start", "gw"), Edge("gw", "big", 1, LargeAmountCondition), Edge("gw", "small", 2)]);

        Assert.Equal("big", string.Join(",", graph.ResolveSelectedTargets(gateway, Amount("2000"))));
        Assert.Equal("small", string.Join(",", graph.ResolveSelectedTargets(gateway, Amount("100"))));

        var pruned = graph.ResolvePrunedTargets(
            gateway,
            States(("big", ApprovalNodeState.Pending), ("small", ApprovalNodeState.Pending)),
            Amount("2000"));
        Assert.Equal("small", string.Join(",", pruned.Select(item => item.NodeKey)));

        // 已推进过的目标不重复剪枝。
        Assert.Empty(graph.ResolvePrunedTargets(
            gateway,
            States(("big", ApprovalNodeState.Pending), ("small", ApprovalNodeState.Completed)),
            Amount("2000")));
    }

    [Fact]
    public void Selected_path_should_preserve_shared_downstream_merge_target()
    {
        var gateway = Node("gw", ApprovalNodeType.ExclusiveGateway, 1);
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), gateway,
                Node("review", ApprovalNodeType.Approval, 2),
                Node("merge", ApprovalNodeType.Approval, 3),
                Node("end", ApprovalNodeType.End, 4)],
            [Edge("start", "gw"), Edge("gw", "review", 1, LargeAmountCondition),
                Edge("gw", "merge", 2), Edge("review", "merge"), Edge("merge", "end")]);

        var pruned = graph.ResolvePrunedTargets(
            gateway,
            States(("review", ApprovalNodeState.Pending), ("merge", ApprovalNodeState.Pending)),
            Amount("2000"));

        Assert.DoesNotContain(pruned, item => item.NodeKey == "merge");
    }

    [Fact]
    public void Single_conditional_edge_should_be_evaluated()
    {
        var approval = Node("approval", ApprovalNodeType.Approval, 1);
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [approval, Node("end", ApprovalNodeType.End, 2)],
            [Edge("approval", "end", 1, LargeAmountCondition)]);

        var exception = Assert.Throws<BusinessException>(
            () => graph.ResolveSelectedTargets(approval, Amount("100")));
        Assert.Equal(ApprovalErrorCodes.DefinitionInvalid, exception.Code);
    }

    [Fact]
    public void Parallel_gateway_should_activate_every_outgoing_edge()
    {
        var fork = Node("fork", ApprovalNodeType.ParallelGateway, 1);
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), fork, Node("a", ApprovalNodeType.Approval, 2),
                Node("b", ApprovalNodeType.Approval, 2)],
            [Edge("start", "fork"), Edge("fork", "a"), Edge("fork", "b")]);

        Assert.Equal("a,b", string.Join(",", graph.ResolveSelectedTargets(fork, Amount("100"))));
        Assert.Empty(graph.ResolvePrunedTargets(
            fork,
            States(("a", ApprovalNodeState.Pending), ("b", ApprovalNodeState.Pending)),
            Amount("100")));
    }

    [Fact]
    public void Exclusive_gateway_should_throw_when_no_outgoing_condition_matches()
    {
        var gateway = Node("gw", ApprovalNodeType.ExclusiveGateway, 1);
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), gateway, Node("a", ApprovalNodeType.Approval, 2),
                Node("b", ApprovalNodeType.Approval, 2)],
            [Edge("start", "gw"), Edge("gw", "a", 1, LargeAmountCondition), Edge("gw", "b", 2, LargeAmountCondition)]);

        var exception = Assert.Throws<BusinessException>(
            () => graph.ResolveSelectedTargets(gateway, Amount("100")));
        Assert.Equal(ApprovalErrorCodes.DefinitionInvalid, exception.Code);
    }

    // ── 发布前校验规则（§8.4）──────────────────────────────────────────────

    [Fact]
    public void Valid_branching_definition_should_pass_validation()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("fork", ApprovalNodeType.ParallelGateway, 1),
                Node("a", ApprovalNodeType.Approval, 2), Node("b", ApprovalNodeType.Approval, 2),
                Node("join", ApprovalNodeType.ParallelGateway, 3), Node("end", ApprovalNodeType.End, 4)],
            [Edge("start", "fork"), Edge("fork", "a"), Edge("fork", "b"), Edge("a", "join"), Edge("b", "join"),
                Edge("join", "end")]);

        Assert.Empty(ApprovalFlowGraphValidator.Validate(graph));
    }

    [Fact]
    public void Loop_back_through_gateway_should_be_rejected()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("gw", ApprovalNodeType.ExclusiveGateway, 1),
                Node("a", ApprovalNodeType.Approval, 2), Node("end", ApprovalNodeType.End, 3)],
            [Edge("start", "gw"), Edge("gw", "a", 1, LargeAmountCondition), Edge("a", "gw", 1), Edge("gw", "end", 2)]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("环路", StringComparison.Ordinal));
    }

    [Fact]
    public void Definition_without_start_or_end_should_be_rejected()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("a", ApprovalNodeType.Approval, 1)],
            [Edge("a", "end")]);

        var problems = ApprovalFlowGraphValidator.Validate(graph);
        Assert.Contains(problems, item => item.Contains("恰好有一个开始节点", StringComparison.Ordinal));
        Assert.Contains(problems, item => item.Contains("至少需要一个结束节点", StringComparison.Ordinal));
    }

    [Fact]
    public void Definition_with_two_start_nodes_should_be_rejected()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start1", ApprovalNodeType.Start, 0), Node("start2", ApprovalNodeType.Start, 0),
                Node("a", ApprovalNodeType.Approval, 1), Node("end", ApprovalNodeType.End, 2)],
            [Edge("start1", "a"), Edge("start2", "a"), Edge("a", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("恰好有一个开始节点，当前有 2 个", StringComparison.Ordinal));
    }

    [Fact]
    public void Unreachable_node_should_be_reported()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("a", ApprovalNodeType.Approval, 1),
                Node("orphan", ApprovalNodeType.Approval, 2), Node("end", ApprovalNodeType.End, 3)],
            [Edge("start", "a"), Edge("a", "end"), Edge("orphan", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("不可达", StringComparison.Ordinal) && item.Contains("orphan", StringComparison.Ordinal));
    }

    [Fact]
    public void Start_node_with_multiple_outgoing_edges_should_be_reported()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("a", ApprovalNodeType.Approval, 1),
                Node("b", ApprovalNodeType.Approval, 1), Node("end", ApprovalNodeType.End, 2)],
            [Edge("start", "a"), Edge("start", "b"), Edge("a", "end"), Edge("b", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("开始节点", StringComparison.Ordinal) && item.Contains("恰好有一条出线", StringComparison.Ordinal));
    }

    [Fact]
    public void Non_end_node_without_outgoing_edge_should_be_reported()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("a", ApprovalNodeType.Approval, 1),
                Node("dead", ApprovalNodeType.Approval, 2), Node("end", ApprovalNodeType.End, 3)],
            [Edge("start", "a"), Edge("a", "dead")]);

        var problems = ApprovalFlowGraphValidator.Validate(graph);
        Assert.Contains(problems, item => item.Contains("没有出线", StringComparison.Ordinal));
        Assert.Contains(problems, item => item.Contains("不可达", StringComparison.Ordinal));
    }
    [Fact]
    public void Cc_node_with_multiple_outgoing_edges_should_be_reported()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("cc", ApprovalNodeType.Cc, 1),
                Node("a", ApprovalNodeType.Approval, 2), Node("b", ApprovalNodeType.Approval, 2),
                Node("end", ApprovalNodeType.End, 3)],
            [Edge("start", "cc"), Edge("cc", "a"), Edge("cc", "b"), Edge("a", "end"), Edge("b", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("不支持多条出线", StringComparison.Ordinal) && item.Contains("[cc]", StringComparison.Ordinal));
    }

    [Fact]
    public void End_node_with_outgoing_edge_should_be_reported()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("a", ApprovalNodeType.Approval, 1),
                Node("end", ApprovalNodeType.End, 2), Node("b", ApprovalNodeType.Approval, 3)],
            [Edge("start", "a"), Edge("a", "end"), Edge("end", "b"), Edge("b", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("结束节点 [end] 不能有出线", StringComparison.Ordinal));
    }

    [Fact]
    public void Approval_node_with_multiple_conditional_outgoing_edges_should_require_default_edge()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("a", ApprovalNodeType.Approval, 1),
                Node("b", ApprovalNodeType.Approval, 2), Node("c", ApprovalNodeType.Approval, 2),
                Node("end", ApprovalNodeType.End, 3)],
            [Edge("start", "a"), Edge("a", "b", 1, LargeAmountCondition), Edge("a", "c", 2, LargeAmountCondition),
                Edge("b", "end"), Edge("c", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("默认出边", StringComparison.Ordinal));
    }

    [Fact]
    public void Parallel_gateway_outgoing_edge_with_condition_should_be_reported()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("fork", ApprovalNodeType.ParallelGateway, 1),
                Node("a", ApprovalNodeType.Approval, 2), Node("b", ApprovalNodeType.Approval, 2),
                Node("end", ApprovalNodeType.End, 3)],
            [Edge("start", "fork"), Edge("fork", "a", 1, LargeAmountCondition), Edge("fork", "b"),
                Edge("a", "end"), Edge("b", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("并行网关", StringComparison.Ordinal) && item.Contains("不能携带条件", StringComparison.Ordinal));
    }

    [Fact]
    public void Exclusive_gateway_with_two_default_edges_should_be_reported()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("gw", ApprovalNodeType.ExclusiveGateway, 1),
                Node("a", ApprovalNodeType.Approval, 2), Node("b", ApprovalNodeType.Approval, 2),
                Node("end", ApprovalNodeType.End, 3)],
            [Edge("start", "gw"), Edge("gw", "a"), Edge("gw", "b"), Edge("a", "end"), Edge("b", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("[gw]", StringComparison.Ordinal) && item.Contains("默认出边", StringComparison.Ordinal));
    }

    [Fact]
    public void Cycle_without_gateway_should_be_reported()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("a", ApprovalNodeType.Approval, 1),
                Node("end", ApprovalNodeType.End, 2)],
            [Edge("start", "a"), Edge("a", "a"), Edge("a", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("环路", StringComparison.Ordinal));
    }

    [Fact]
    public void Definition_without_approval_node_should_be_rejected()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("end", ApprovalNodeType.End, 1)],
            [Edge("start", "end")]);

        Assert.Contains(
            ApprovalFlowGraphValidator.Validate(graph),
            item => item.Contains("至少需要一个审批节点", StringComparison.Ordinal));
    }

    [Fact]
    public void Ensure_valid_should_throw_definition_invalid_with_all_problems()
    {
        var graph = ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("a", ApprovalNodeType.Approval, 1)],
            [Edge("a", "b")]);

        var exception = Assert.Throws<BusinessException>(() => ApprovalFlowGraphValidator.EnsureValid(graph));
        Assert.Equal(ApprovalErrorCodes.DefinitionInvalid, exception.Code);
        Assert.Contains("开始节点", exception.Message, StringComparison.Ordinal);
    }

    // ── 测试辅助 ──────────────────────────────────────────────────────────

    /// <summary>构造 <c>start → (a | b) → join</c> 的双入边合流图。</summary>
    private static ApprovalFlowGraph BuildSplitJoinGraph(ApprovalJoinPolicy joinPolicy) =>
        ApprovalFlowGraph.Build(
            Guid.NewGuid(),
            [Node("start", ApprovalNodeType.Start, 0), Node("a", ApprovalNodeType.Approval, 1),
                Node("b", ApprovalNodeType.Approval, 1),
                Node("join", ApprovalNodeType.Approval, 2, joinPolicy)],
            [Edge("start", "a"), Edge("start", "b"), Edge("a", "join"), Edge("b", "join")]);

    private static IReadOnlyList<string> Validate(
        IReadOnlyList<ApprovalFlowNode> nodes,
        IReadOnlyList<ApprovalFlowEdge> edges) =>
        ApprovalFlowGraphValidator.Validate(ApprovalFlowGraph.Build(Guid.NewGuid(), nodes, edges));

    private static ApprovalFlowNode Node(
        string nodeKey,
        ApprovalNodeType nodeType,
        int sortOrder,
        ApprovalJoinPolicy joinPolicy = ApprovalJoinPolicy.AnyArrival,
        string? conditionJson = null) => new()
    {
        Id = Guid.NewGuid(),
        NodeKey = nodeKey,
        Name = nodeKey,
        NodeType = nodeType,
        SortOrder = sortOrder,
        JoinPolicy = joinPolicy,
        ConditionJson = conditionJson
    };

    private static ApprovalFlowEdge Edge(
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

    private static Dictionary<string, ApprovalNodeState> States(
        params (string Key, ApprovalNodeState State)[] entries) =>
        entries.ToDictionary(item => item.Key, item => item.State, StringComparer.Ordinal);

    private static Dictionary<string, string> Amount(string value) =>
        new(StringComparer.Ordinal) { ["amount"] = value };
}
