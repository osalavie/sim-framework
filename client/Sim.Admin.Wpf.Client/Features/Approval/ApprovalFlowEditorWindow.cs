using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Controls.Inputs;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.Approval;

public sealed class ApprovalFlowEditorWindow : Window
{
    private enum PortSide { Top, Right, Bottom, Left }
    private enum LineStyle { Straight, Orthogonal, Bezier }
    private sealed record FlowPort(string NodeKey, PortSide Side);
    private sealed record EdgePorts(PortSide Source, PortSide Target, LineStyle Style);
    private static readonly PortSide[] PortSides =
        [PortSide.Top, PortSide.Right, PortSide.Bottom, PortSide.Left];

    private static readonly string[] NodeTypes =
        ["Start", "Approval", "Cc", "ExclusiveGateway", "ParallelGateway", "End"];
    private static readonly string[] ApproverTypes =
        ["Initiator", "SpecifiedUsers", "Role", "OrganizationManager", "OrganizationMembers"];
    private static readonly string[] MultiModes = ["All", "Any", "Sequential"];
    private static readonly string[] JoinPolicies = ["AnyArrival", "AllArrival"];

    private readonly ApiClient _api;
    private readonly Action _sessionExpired;
    private readonly ApprovalFlowDefinitionDetail _definition;
    private readonly bool _editable;
    private readonly CustomInput _name = new() { Margin = new Thickness(0, 0, 0, 10) };
    private readonly CustomInput _businessType = new() { Margin = new Thickness(0, 0, 0, 10) };
    private readonly CustomTextarea _description = new() { Height = 72, Margin = new Thickness(0, 0, 0, 10) };
    private readonly CustomInput _withdrawHours = new() { Margin = new Thickness(0, 0, 0, 10) };
    private readonly CustomInput _fallbackUsers = new() { Margin = new Thickness(0, 0, 0, 10) };
    private readonly CustomCheckBox _allowWithdraw = new() { Content = "允许撤回" };
    private readonly CustomCheckBox _autoPass = new() { Content = "审批人与发起人相同时自动通过" };
    private readonly Canvas _canvas = new() { Width = 780, Height = 480 };
    private readonly Dictionary<string, Point> _positions = [];
    private readonly Dictionary<string, Dictionary<PortSide, Grid>> _nodePorts = [];
    private readonly Dictionary<ApprovalFlowEdge, EdgePorts> _edgePorts = [];
    private readonly List<(ApprovalFlowEdge Edge, Path Visible, Path HitArea)> _edgeVisuals = [];
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private Border? _draggedNode;
    private Point _dragOffset;
    private bool _nodeMoved;
    private FlowPort? _connectingFrom;
    private Line? _connectionPreview;
    private ApprovalFlowEdge? _selectedEdge;
    private string? _selectedNodeKey;
    private bool _busy;
    private bool _dirty;

    public bool Changed { get; private set; }

    public ApprovalFlowEditorWindow(ApiClient api, ApprovalFlowDefinitionDetail definition,
        Action sessionExpired)
    {
        _api = api;
        _definition = definition;
        _sessionExpired = sessionExpired;
        _editable = definition.Status == "Draft";
        Title = $"流程定义 · {definition.Name} v{definition.Version}";
        Width = 900;
        Height = 750;
        MinWidth = 640;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "PageBrush");
        SetResourceReference(ForegroundProperty, "InkBrush");
        _status.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");

        _name.Text = definition.Name ?? string.Empty;
        _businessType.Text = definition.BusinessType ?? string.Empty;
        _description.Text = definition.Description ?? string.Empty;
        _withdrawHours.Text = definition.WithdrawWindowHours?.ToString() ?? string.Empty;
        _fallbackUsers.Text = string.Join(", ", definition.FallbackApproverUserIds);
        _allowWithdraw.IsChecked = definition.AllowWithdraw;
        _autoPass.IsChecked = definition.AutoPassWhenInitiator;
        LoadPositions();

        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(new TextBlock
        {
            Text = $"{definition.Code} · v{definition.Version} · {definition.Status}",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 12)
        });
        if (!_editable)
            content.Children.Add(new TextBlock
            {
                Text = "已发布或停用的版本只读。请在流程定义列表升版后编辑草稿。",
                Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap
            });
        content.Children.Add(FieldLabel("流程名称 *"));
        content.Children.Add(_name);
        content.Children.Add(FieldLabel("业务类型"));
        content.Children.Add(_businessType);
        content.Children.Add(FieldLabel("说明"));
        content.Children.Add(_description);
        var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        options.Children.Add(_allowWithdraw);
        _autoPass.Margin = new Thickness(20, 0, 0, 0);
        options.Children.Add(_autoPass);
        content.Children.Add(options);
        content.Children.Add(FieldLabel("撤回时间窗（小时，1–8760；留空不限）"));
        content.Children.Add(_withdrawHours);
        content.Children.Add(FieldLabel("兜底审批人 ID（逗号分隔）"));
        content.Children.Add(_fallbackUsers);

        content.Children.Add(FieldLabel("流程画布"));
        var palette = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };
        foreach (var (type, label) in new[]
                 {
                     ("Approval", "+ 审批"), ("Cc", "+ 抄送"),
                     ("ExclusiveGateway", "+ 条件网关"), ("ParallelGateway", "+ 并行网关"),
                     ("Start", "+ 开始"), ("End", "+ 结束")
                 })
        {
            var add = Button(label, (_, _) => AddCanvasNode(type), false);
            add.IsEnabled = _editable;
            palette.Children.Add(add);
        }
        content.Children.Add(palette);
        var canvasHint = new TextBlock
        {
            Text = "拖动节点排版；从任一边连接点拖到目标节点任一边建立连线；双击编辑，右键连线可切换线型与分支优先级。",
            Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap
        };
        canvasHint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        content.Children.Add(canvasHint);
        _canvas.Background = Brushes.Transparent;
        _canvas.MouseMove += CanvasConnectionMove;
        _canvas.MouseLeftButtonUp += CanvasConnectionUp;
        _canvas.MouseLeave += (_, _) => UpdatePortVisibility(new Point(double.NaN, double.NaN));
        _canvas.ContextMenu = CreateCanvasMenu();
        var canvasFrame = new Border
        {
            Child = _canvas,
            BorderThickness = new Thickness(2),
            Margin = new Thickness(0, 0, 0, 10)
        };
        canvasFrame.SetResourceReference(Border.BorderBrushProperty, "EdgeBrush");
        canvasFrame.SetResourceReference(Border.BackgroundProperty, "PageDeepBrush");
        content.Children.Add(new ScrollViewer
        {
            Content = canvasFrame,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 480
        });

        var save = Button("保存草稿", async (_, _) => await SaveAsync(), true);
        save.IsEnabled = _editable;
        var validate = Button("校验已保存版本", async (_, _) => await ValidateAsync(), false);
        var close = Button("关闭", (_, _) => Close(), false);
        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 8)
        };
        footer.Children.Add(save);
        footer.Children.Add(validate);
        footer.Children.Add(close);
        content.Children.Add(footer);
        content.Children.Add(_status);
        Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        PreviewKeyDown += EditorKeyDown;
        foreach (var control in new Control[] { _name, _businessType, _description,
                     _withdrawHours, _fallbackUsers, _allowWithdraw, _autoPass })
            control.IsEnabled = _editable;
        foreach (var input in new TextBox[] { _name, _businessType, _description,
                     _withdrawHours, _fallbackUsers })
            input.TextChanged += (_, _) => MarkDirty();
        _allowWithdraw.Checked += (_, _) => MarkDirty();
        _allowWithdraw.Unchecked += (_, _) => MarkDirty();
        _autoPass.Checked += (_, _) => MarkDirty();
        _autoPass.Unchecked += (_, _) => MarkDirty();
        Closing += (_, args) =>
        {
            if (!_dirty || MessageBox.Show(this, "还有未保存的流程修改，确定放弃吗？",
                    "放弃修改", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                return;
            args.Cancel = true;
        };
        RenderCanvas();
    }

    private static TextBlock FieldLabel(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 8, 0, 5)
    };

    private static CustomButton Button(string text, RoutedEventHandler click, bool primary)
    {
        var button = new CustomButton
        {
            Content = text, Margin = new Thickness(8, 0, 0, 0), MinWidth = 90,
            Variant = primary ? CustomButtonVariant.Primary : CustomButtonVariant.Secondary
        };
        button.Click += click;
        return button;
    }

    private void AddCanvasNode(string type)
    {
        if (!_editable) return;
        if ((type is "Start" or "End") && _definition.Nodes.Any(node => node.NodeType == type))
        {
            _status.Text = type == "Start" ? "流程已有开始节点。" : "流程已有结束节点。";
            return;
        }
        var prefix = char.ToLowerInvariant(type[0]) + type[1..];
        var number = 1;
        while (_definition.Nodes.Any(node => node.NodeKey == $"{prefix}{number}")) number++;
        var label = type switch
        {
            "Approval" => "审批", "Cc" => "抄送", "Start" => "开始", "End" => "结束",
            "ExclusiveGateway" => "条件网关", _ => "并行网关"
        };
        EditNodeCore(new ApprovalFlowNode
        {
            NodeKey = $"{prefix}{number}", Name = label, NodeType = type
        }, -1);
    }

    private void EditNodeCore(ApprovalFlowNode node, int index)
    {
        var values = CustomPromptDialog.Show(this, index < 0 ? "新增节点" : "编辑节点",
            "节点类型：Start / Approval / Cc / ExclusiveGateway / ParallelGateway / End。审批人来源：Initiator / SpecifiedUsers / Role / OrganizationManager / OrganizationMembers。",
            [new("节点标识", Value: node.NodeKey), new("名称", Value: node.Name),
                new("节点类型", Value: node.NodeType), new("审批人来源", Value: node.ApproverType),
                new("指定审批人 ID（逗号分隔）", Value: string.Join(", ", node.ApproverUserIds), Optional: true),
                new("审批角色", Value: node.ApproverRoleName, Optional: true),
                new("审批组织 ID", Value: node.ApproverOrganizationId?.ToString(), Optional: true),
                new("多人审批模式（All / Any / Sequential）", Value: node.MultiApproveMode),
                new("汇聚策略（AnyArrival / AllArrival）", Value: node.JoinPolicy),
                new("节点条件 JSON", Multiline: true, Value: node.ConditionJson, Optional: true),
                new("超时小时数", Value: node.TimeoutHours?.ToString(), Optional: true),
                new("抄送配置 JSON", Multiline: true, Value: node.CcConfigJson, Optional: true),
                new("允许转交（true / false）", Value: node.AllowTransfer.ToString().ToLowerInvariant()),
                new("允许加签（true / false）", Value: node.AllowAddSign.ToString().ToLowerInvariant()),
                new("允许退回发起人（true / false）", Value: node.AllowReturnToInitiator.ToString().ToLowerInvariant())],
            index < 0 ? "新增" : "保存");
        if (values is null) return;
        try
        {
            var key = values[0].Trim();
            if (_definition.Nodes.Where((_, i) => i != index).Any(item => item.NodeKey == key))
                throw new InvalidOperationException("节点标识不能重复。");
            if (!NodeTypes.Contains(values[2].Trim()) || !ApproverTypes.Contains(values[3].Trim()) ||
                !MultiModes.Contains(values[7].Trim()) || !JoinPolicies.Contains(values[8].Trim()))
                throw new InvalidOperationException("节点类型、审批人来源或审批模式无效。");
            if ((values[2].Trim() is "Start" or "End") &&
                _definition.Nodes.Where((_, i) => i != index).Any(item => item.NodeType == values[2].Trim()))
                throw new InvalidOperationException("流程只能有一个开始节点和一个结束节点。");
            var userIds = ParseIds(values[4]);
            Guid? organization = string.IsNullOrWhiteSpace(values[6]) ? null : Guid.Parse(values[6].Trim());
            int? timeout = string.IsNullOrWhiteSpace(values[10]) ? null : int.Parse(values[10].Trim());
            if (timeout is < 1 or > 8760) throw new InvalidOperationException("超时小时数须为 1–8760。");
            ValidateJson(values[9]);
            ValidateJson(values[11]);
            var transfer = bool.Parse(values[12]);
            var addSign = bool.Parse(values[13]);
            var returnToInitiator = bool.Parse(values[14]);
            var oldKey = node.NodeKey;
            node.NodeKey = key;
            node.Name = values[1].Trim();
            node.NodeType = values[2].Trim();
            node.ApproverType = values[3].Trim();
            node.ApproverUserIds = userIds;
            node.ApproverRoleName = EmptyToNull(values[5]);
            node.ApproverOrganizationId = organization;
            node.MultiApproveMode = values[7].Trim();
            node.JoinPolicy = values[8].Trim();
            node.ConditionJson = EmptyToNull(values[9]);
            node.TimeoutHours = timeout;
            node.CcConfigJson = EmptyToNull(values[11]);
            node.AllowTransfer = transfer;
            node.AllowAddSign = addSign;
            node.AllowReturnToInitiator = returnToInitiator;
            if (index < 0) _definition.Nodes.Add(node);
            else if (oldKey != key)
            {
                if (_positions.Remove(oldKey, out var position)) _positions[key] = position;
                foreach (var edge in _definition.Edges)
                {
                    if (edge.FromNodeKey == oldKey) edge.FromNodeKey = key;
                    if (edge.ToNodeKey == oldKey) edge.ToNodeKey = key;
                }
            }
            _selectedNodeKey = key;
            RenderCanvas();
            MarkDirty();
        }
        catch (Exception error) when (error is FormatException or InvalidOperationException or JsonException)
        {
            _status.Text = $"节点未保存：{error.Message}";
        }
    }

    private void DeleteNode(string key)
    {
        var index = _definition.Nodes.FindIndex(node => node.NodeKey == key);
        if (index < 0) return;
        var node = _definition.Nodes[index];
        if (MessageBox.Show(this, $"删除节点“{node.Name}”及其连线？", "删除节点",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _definition.Nodes.RemoveAt(index);
        _positions.Remove(node.NodeKey);
        var removedEdges = _definition.Edges.Where(edge => edge.FromNodeKey == node.NodeKey ||
            edge.ToNodeKey == node.NodeKey).ToList();
        _definition.Edges.RemoveAll(removedEdges.Contains);
        foreach (var edge in removedEdges) _edgePorts.Remove(edge);
        _selectedNodeKey = null;
        if (_selectedEdge is not null && removedEdges.Contains(_selectedEdge)) _selectedEdge = null;
        RenderCanvas();
        MarkDirty();
    }

    private void EditEdgeCore(ApprovalFlowEdge edge, int index)
    {
        var values = CustomPromptDialog.Show(this, index < 0 ? "新增连线" : "编辑连线",
            "起点和终点必须填写已有节点标识；同一起点的连线按创建顺序求值，可在画布右键调整优先级。",
            [new("起点节点标识", Value: edge.FromNodeKey),
                new("终点节点标识", Value: edge.ToNodeKey),
                new("标签", Value: edge.Label, Optional: true),
                new("条件 JSON", Multiline: true, Value: edge.ConditionJson, Optional: true),
                new("线型（Straight / Orthogonal / Bezier）",
                    Value: PortsOf(edge).Style.ToString())],
            index < 0 ? "新增" : "保存");
        if (values is null) return;
        try
        {
            var from = values[0].Trim();
            var to = values[1].Trim();
            if (from == to || !_definition.Nodes.Any(node => node.NodeKey == from) ||
                !_definition.Nodes.Any(node => node.NodeKey == to))
                throw new InvalidOperationException("请选择两个不同的现有节点。");
            if (_definition.Nodes.First(node => node.NodeKey == from).NodeType == "End" ||
                _definition.Nodes.First(node => node.NodeKey == to).NodeType == "Start")
                throw new InvalidOperationException("结束节点不能引出连线，开始节点不能接收连线。");
            if (_definition.Edges.Where((_, i) => i != index)
                .Any(item => item.FromNodeKey == from && item.ToNodeKey == to))
                throw new InvalidOperationException("该连线已存在。");
            ValidateJson(values[3]);
            if (!Enum.TryParse<LineStyle>(values[4].Trim(), true, out var style) ||
                !Enum.IsDefined(style))
                throw new InvalidOperationException("请选择直线、正交线或贝塞尔曲线。");
            if (edge.FromNodeKey != from || edge.ToNodeKey != to)
                _edgePorts.Remove(edge);
            edge.FromNodeKey = from;
            edge.ToNodeKey = to;
            edge.Label = EmptyToNull(values[2]);
            edge.ConditionJson = EmptyToNull(values[3]);
            if (index < 0) _definition.Edges.Add(edge);
            _edgePorts[edge] = PortsOf(edge) with { Style = style };
            _selectedEdge = edge;
            _selectedNodeKey = null;
            RenderCanvas();
            MarkDirty();
        }
        catch (Exception error) when (error is InvalidOperationException or JsonException)
        {
            _status.Text = $"连线未保存：{error.Message}";
        }
    }

    private async Task SaveAsync()
    {
        if (_busy || !_editable) return;
        try
        {
            _definition.Name = _name.Text.Trim();
            if (_definition.Name.Length == 0) throw new InvalidOperationException("请填写流程名称。");
            _definition.BusinessType = EmptyToNull(_businessType.Text);
            _definition.Description = EmptyToNull(_description.Text);
            _definition.AllowWithdraw = _allowWithdraw.IsChecked == true;
            _definition.AutoPassWhenInitiator = _autoPass.IsChecked == true;
            _definition.WithdrawWindowHours = string.IsNullOrWhiteSpace(_withdrawHours.Text)
                ? null : int.Parse(_withdrawHours.Text.Trim());
            if (_definition.WithdrawWindowHours is < 1 or > 8760)
                throw new InvalidOperationException("撤回时间窗须为 1–8760 小时。");
            _definition.FallbackApproverUserIds = ParseIds(_fallbackUsers.Text);
            if (_definition.Nodes.Count == 0) throw new InvalidOperationException("流程至少需要一个节点。");
            if (_definition.Nodes.Any(node => node.NodeType == "Approval") == false)
                throw new InvalidOperationException("流程至少需要一个审批节点。");
            _definition.Nodes = _definition.Nodes
                .OrderBy(node => _positions[node.NodeKey].Y)
                .ThenBy(node => _positions[node.NodeKey].X)
                .ToList();
            _definition.LayoutJson = BuildLayout();
            _busy = true;
            _status.Text = "正在保存…";
            await _api.UpdateApprovalDefinitionAsync(_definition);
            Changed = true;
            _dirty = false;
            _status.Text = "草稿已保存。请运行服务端校验。";
        }
        catch (SessionExpiredException) { _dirty = false; _sessionExpired(); Close(); }
        catch (Exception error) { _status.Text = $"保存失败：{error.Message}"; }
        finally { _busy = false; }
    }

    private async Task ValidateAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var result = await _api.ValidateApprovalDefinitionAsync(_definition.Id);
            _status.Text = result.Valid ? "服务端校验通过。" : string.Join("；", result.Problems);
        }
        catch (SessionExpiredException) { _dirty = false; _sessionExpired(); Close(); }
        catch (Exception error) { _status.Text = $"校验失败：{error.Message}"; }
        finally { _busy = false; }
    }

    private string BuildLayout()
    {
        var positions = new Dictionary<string, object>();
        for (var i = 0; i < _definition.Nodes.Count; i++)
        {
            var key = _definition.Nodes[i].NodeKey;
            var point = PositionOf(key, i);
            positions[key] = new { x = Math.Round(point.X), y = Math.Round(point.Y) };
        }
        // Web 的布局解析只读取含 x/y 的节点项；保留桌面端的连接方位和线型。
        positions["__edgePorts"] = _definition.Edges.ToDictionary(
            edge => $"{edge.FromNodeKey}->{edge.ToNodeKey}",
            edge =>
            {
                var sides = PortsOf(edge);
                return new
                {
                    source = sides.Source.ToString(),
                    target = sides.Target.ToString(),
                    style = sides.Style.ToString()
                };
            });
        return JsonSerializer.Serialize(positions);
    }

    private void MarkDirty()
    {
        if (!_editable) return;
        _dirty = true;
        _status.Text = "有未保存的流程修改。";
    }

    private void LoadPositions()
    {
        if (string.IsNullOrWhiteSpace(_definition.LayoutJson)) return;
        try
        {
            using var document = JsonDocument.Parse(_definition.LayoutJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name == "__edgePorts" && property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var edge in _definition.Edges)
                    {
                        var key = $"{edge.FromNodeKey}->{edge.ToNodeKey}";
                        if (!property.Value.TryGetProperty(key, out var value) ||
                            value.ValueKind != JsonValueKind.Object ||
                            !value.TryGetProperty("source", out var source) ||
                            !value.TryGetProperty("target", out var target) ||
                            source.ValueKind != JsonValueKind.String ||
                            target.ValueKind != JsonValueKind.String ||
                            !Enum.TryParse<PortSide>(source.GetString(), true, out var sourceSide) ||
                            !Enum.TryParse<PortSide>(target.GetString(), true, out var targetSide) ||
                            !Enum.IsDefined(sourceSide) || !Enum.IsDefined(targetSide)) continue;
                        var style = value.TryGetProperty("style", out var styleValue) &&
                                    styleValue.ValueKind == JsonValueKind.String &&
                                    Enum.TryParse<LineStyle>(styleValue.GetString(), true, out var parsedStyle) &&
                                    Enum.IsDefined(parsedStyle)
                            ? parsedStyle : LineStyle.Straight;
                        _edgePorts[edge] = new EdgePorts(sourceSide, targetSide, style);
                    }
                    continue;
                }
                if (property.Value.ValueKind != JsonValueKind.Object ||
                    !property.Value.TryGetProperty("x", out var x) ||
                    !property.Value.TryGetProperty("y", out var y) ||
                    !x.TryGetDouble(out var left) || !y.TryGetDouble(out var top) ||
                    !double.IsFinite(left) || !double.IsFinite(top)) continue;
                _positions[property.Name] = new Point(Math.Max(14, left), Math.Max(14, top));
            }
        }
        catch (JsonException) { }
    }

    private Point PositionOf(string key, int index)
    {
        if (_positions.TryGetValue(key, out var point)) return point;
        point = new Point(260, 40 + index * 132);
        _positions[key] = point;
        return point;
    }

    private EdgePorts PortsOf(ApprovalFlowEdge edge)
    {
        if (_edgePorts.TryGetValue(edge, out var ports)) return ports;
        var fromIndex = _definition.Nodes.FindIndex(node => node.NodeKey == edge.FromNodeKey);
        var toIndex = _definition.Nodes.FindIndex(node => node.NodeKey == edge.ToNodeKey);
        if (fromIndex < 0 || toIndex < 0)
            return new EdgePorts(PortSide.Bottom, PortSide.Top, LineStyle.Straight);
        var from = PositionOf(edge.FromNodeKey, fromIndex);
        var to = PositionOf(edge.ToNodeKey, toIndex);
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        ports = Math.Abs(deltaX) > Math.Abs(deltaY)
            ? deltaX >= 0 ? new EdgePorts(PortSide.Right, PortSide.Left, LineStyle.Straight)
                : new EdgePorts(PortSide.Left, PortSide.Right, LineStyle.Straight)
            : deltaY >= 0 ? new EdgePorts(PortSide.Bottom, PortSide.Top, LineStyle.Straight)
                : new EdgePorts(PortSide.Top, PortSide.Bottom, LineStyle.Straight);
        _edgePorts[edge] = ports;
        return ports;
    }

    private Point PortPoint(string key, int index, PortSide side)
    {
        var point = PositionOf(key, index);
        return side switch
        {
            PortSide.Top => new Point(point.X + 75, point.Y),
            PortSide.Right => new Point(point.X + 150, point.Y + 34),
            PortSide.Bottom => new Point(point.X + 75, point.Y + 68),
            _ => new Point(point.X, point.Y + 34)
        };
    }

    private static Point Outside(Point point, PortSide side, double distance) => side switch
    {
        PortSide.Top => new Point(point.X, point.Y - distance),
        PortSide.Right => new Point(point.X + distance, point.Y),
        PortSide.Bottom => new Point(point.X, point.Y + distance),
        _ => new Point(point.X - distance, point.Y)
    };

    private static Geometry EdgeGeometry(Point from, Point to, EdgePorts ports)
    {
        if (ports.Style == LineStyle.Straight) return new LineGeometry(from, to);
        var figure = new PathFigure { StartPoint = from, IsClosed = false, IsFilled = false };
        if (ports.Style == LineStyle.Bezier)
        {
            var distance = Math.Clamp((Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y)) / 3,
                48, 160);
            figure.Segments.Add(new BezierSegment(
                Outside(from, ports.Source, distance),
                Outside(to, ports.Target, distance), to, true));
        }
        else
        {
            const double clearance = 28;
            var start = Outside(from, ports.Source, clearance);
            var end = Outside(to, ports.Target, clearance);
            var points = new PointCollection { start };
            if (ports.Source is PortSide.Left or PortSide.Right)
            {
                var middleX = (start.X + end.X) / 2;
                points.Add(new Point(middleX, start.Y));
                points.Add(new Point(middleX, end.Y));
            }
            else
            {
                var middleY = (start.Y + end.Y) / 2;
                points.Add(new Point(start.X, middleY));
                points.Add(new Point(end.X, middleY));
            }
            points.Add(end);
            points.Add(to);
            figure.Segments.Add(new PolyLineSegment(points, true));
        }
        return new PathGeometry(new PathFigureCollection { figure });
    }

    private void RenderCanvas()
    {
        _canvas.Children.Clear();
        _edgeVisuals.Clear();
        _nodePorts.Clear();
        var nodeIndex = _definition.Nodes.Select((node, index) => (node, index))
            .ToDictionary(item => item.node.NodeKey, item => item.index);
        var maxX = 780d;
        var maxY = 480d;
        foreach (var (node, index) in _definition.Nodes.Select((node, index) => (node, index)))
        {
            var point = PositionOf(node.NodeKey, index);
            maxX = Math.Max(maxX, point.X + 180);
            maxY = Math.Max(maxY, point.Y + 100);
        }
        _canvas.Width = maxX;
        _canvas.Height = maxY;
        foreach (var edge in _definition.Edges)
        {
            if (!nodeIndex.TryGetValue(edge.FromNodeKey, out var fromIndex) ||
                !nodeIndex.TryGetValue(edge.ToNodeKey, out var toIndex)) continue;
            var ports = PortsOf(edge);
            var from = PortPoint(edge.FromNodeKey, fromIndex, ports.Source);
            var to = PortPoint(edge.ToNodeKey, toIndex, ports.Target);
            var geometry = EdgeGeometry(from, to, ports);
            var line = new Path
            {
                Data = geometry,
                StrokeThickness = 2,
                IsHitTestVisible = false
            };
            line.SetResourceReference(Shape.StrokeProperty,
                ReferenceEquals(edge, _selectedEdge) ? "ActionBrush" : "PrimaryBrush");
            _canvas.Children.Add(line);
            var hitArea = new Path
            {
                Data = geometry,
                StrokeThickness = 16, Stroke = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            hitArea.MouseLeftButtonDown += CanvasEdgeDown;
            if (_editable) hitArea.ContextMenu = CreateEdgeMenu(edge);
            _canvas.Children.Add(hitArea);
            _edgeVisuals.Add((edge, line, hitArea));
        }
        foreach (var (node, index) in _definition.Nodes.Select((node, index) => (node, index)))
        {
            var point = PositionOf(node.NodeKey, index);
            var card = new Border
            {
                Width = 150, Height = 68,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Tag = node.NodeKey,
                Cursor = _editable ? Cursors.SizeAll : Cursors.Arrow,
                Child = new StackPanel
                {
                    Margin = new Thickness(8, 6, 8, 6),
                    Children =
                    {
                        new TextBlock { Text = node.Name, FontWeight = FontWeights.Bold,
                            TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = $"{node.NodeType} · {node.NodeKey}", FontSize = 11,
                            TextTrimming = TextTrimming.CharacterEllipsis }
                    }
                }
            };
            card.SetResourceReference(Border.BorderBrushProperty,
                node.NodeKey == _selectedNodeKey ? "ActionBrush" : "EdgeBrush");
            card.SetResourceReference(Border.BackgroundProperty, "PageBrush");
            card.MouseLeftButtonDown += CanvasNodeDown;
            card.MouseEnter += (_, e) => UpdatePortVisibility(e.GetPosition(_canvas));
            card.MouseMove += CanvasNodeMove;
            card.MouseLeftButtonUp += CanvasNodeUp;
            if (_editable) card.ContextMenu = CreateNodeMenu(node.NodeKey);
            Canvas.SetLeft(card, point.X);
            Canvas.SetTop(card, point.Y);
            _canvas.Children.Add(card);
            if (_editable)
            {
                var ports = new Dictionary<PortSide, Grid>();
                foreach (var side in PortSides)
                {
                    var port = CreatePort(node.NodeKey, side);
                    ports[side] = port;
                    _canvas.Children.Add(port);
                }
                _nodePorts[node.NodeKey] = ports;
                PositionPorts(node.NodeKey, index);
            }
        }
        UpdatePortVisibility(Mouse.GetPosition(_canvas));
    }

    private Grid CreatePort(string key, PortSide side)
    {
        var dot = new Ellipse
        {
            Width = 8, Height = 8, StrokeThickness = 1.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        dot.SetResourceReference(Shape.FillProperty, "ActionBrush");
        dot.SetResourceReference(Shape.StrokeProperty, "EdgeBrush");
        var port = new Grid
        {
            Width = 20, Height = 20,
            Background = Brushes.Transparent,
            Visibility = Visibility.Collapsed,
            Tag = new FlowPort(key, side),
            Cursor = Cursors.Cross,
            ToolTip = $"{SideLabel(side)}连接点：按住拖到另一节点的任意连接点",
            Children = { dot }
        };
        port.MouseLeftButtonDown += CanvasPortDown;
        return port;
    }

    private void PositionPorts(string key, int index)
    {
        if (!_nodePorts.TryGetValue(key, out var ports)) return;
        foreach (var (side, port) in ports)
        {
            var center = PortPoint(key, index, side);
            Canvas.SetLeft(port, center.X - 10);
            Canvas.SetTop(port, center.Y - 10);
        }
    }

    private void UpdatePortVisibility(Point pointer)
    {
        if (!_editable) return;
        foreach (var (key, ports) in _nodePorts)
        {
            var index = _definition.Nodes.FindIndex(node => node.NodeKey == key);
            if (index < 0) continue;
            var point = PositionOf(key, index);
            var hovered = pointer.X >= point.X - 14 && pointer.X <= point.X + 164 &&
                          pointer.Y >= point.Y - 14 && pointer.Y <= point.Y + 82;
            var active = _connectingFrom?.NodeKey == key ||
                         _draggedNode?.Tag is string draggingKey && draggingKey == key;
            foreach (var port in ports.Values)
                port.Visibility = hovered || active ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private ContextMenu CreateCanvasMenu()
    {
        var menu = new ContextMenu();
        if (!_editable) return menu;
        foreach (var (type, label) in new[]
                 {
                     ("Approval", "新增审批节点"), ("Cc", "新增抄送节点"),
                     ("ExclusiveGateway", "新增条件网关"),
                     ("ParallelGateway", "新增并行网关"),
                     ("Start", "新增开始节点"), ("End", "新增结束节点")
                 })
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => AddCanvasNode(type);
            menu.Items.Add(item);
        }
        return menu;
    }

    private static string SideLabel(PortSide side) => side switch
    {
        PortSide.Top => "上边",
        PortSide.Right => "右边",
        PortSide.Bottom => "下边",
        _ => "左边"
    };

    private ContextMenu CreateNodeMenu(string key)
    {
        var menu = new ContextMenu();
        var edit = new MenuItem { Header = "编辑节点" };
        edit.Click += (_, _) => EditCanvasNode(key);
        var delete = new MenuItem { Header = "删除节点" };
        delete.Click += (_, _) => DeleteCanvasNode(key);
        menu.Items.Add(edit);
        menu.Items.Add(delete);
        return menu;
    }

    private ContextMenu CreateEdgeMenu(ApprovalFlowEdge edge)
    {
        var menu = new ContextMenu();
        var edit = new MenuItem { Header = "编辑连线" };
        edit.Click += (_, _) => EditCanvasEdge(edge);
        var delete = new MenuItem { Header = "删除连线" };
        delete.Click += (_, _) => DeleteCanvasEdge(edge);
        menu.Items.Add(edit);
        menu.Items.Add(delete);
        var styles = new MenuItem { Header = "连线样式" };
        foreach (var (style, label) in new[]
                 {
                     (LineStyle.Straight, "直线"),
                     (LineStyle.Orthogonal, "正交线"),
                     (LineStyle.Bezier, "贝塞尔曲线")
                 })
        {
            var item = new MenuItem
            {
                Header = label,
                IsCheckable = true,
                IsChecked = PortsOf(edge).Style == style
            };
            item.Click += (_, _) =>
            {
                _edgePorts[edge] = PortsOf(edge) with { Style = style };
                _selectedEdge = edge;
                _selectedNodeKey = null;
                RenderCanvas();
                MarkDirty();
            };
            styles.Items.Add(item);
        }
        menu.Items.Add(styles);
        if (_definition.Edges.Count(item => item.FromNodeKey == edge.FromNodeKey) > 1)
        {
            var moveUp = new MenuItem { Header = "提高分支优先级" };
            moveUp.Click += (_, _) => MoveEdge(edge, -1);
            var moveDown = new MenuItem { Header = "降低分支优先级" };
            moveDown.Click += (_, _) => MoveEdge(edge, 1);
            menu.Items.Add(moveUp);
            menu.Items.Add(moveDown);
        }
        return menu;
    }

    private void MoveEdge(ApprovalFlowEdge edge, int direction)
    {
        var siblings = _definition.Edges.Where(item => item.FromNodeKey == edge.FromNodeKey).ToList();
        var siblingIndex = siblings.IndexOf(edge);
        var targetIndex = siblingIndex + direction;
        if (siblingIndex < 0 || targetIndex < 0 || targetIndex >= siblings.Count) return;
        var index = _definition.Edges.IndexOf(edge);
        var otherIndex = _definition.Edges.IndexOf(siblings[targetIndex]);
        (_definition.Edges[index], _definition.Edges[otherIndex]) =
            (_definition.Edges[otherIndex], _definition.Edges[index]);
        _selectedEdge = edge;
        _selectedNodeKey = null;
        RenderCanvas();
        MarkDirty();
    }

    private void EditCanvasNode(string key)
    {
        var index = _definition.Nodes.FindIndex(node => node.NodeKey == key);
        if (index < 0) return;
        _selectedNodeKey = key;
        EditNodeCore(_definition.Nodes[index], index);
    }

    private void DeleteCanvasNode(string key) => DeleteNode(key);

    private void EditCanvasEdge(ApprovalFlowEdge edge)
    {
        var index = _definition.Edges.IndexOf(edge);
        if (index < 0) return;
        EditEdgeCore(edge, index);
    }

    private void DeleteCanvasEdge(ApprovalFlowEdge edge)
    {
        var index = _definition.Edges.IndexOf(edge);
        if (index < 0) return;
        _definition.Edges.RemoveAt(index);
        _edgePorts.Remove(edge);
        _selectedEdge = null;
        RenderCanvas();
        MarkDirty();
    }

    private void CanvasNodeDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border card || card.Tag is not string key) return;
        var index = _definition.Nodes.FindIndex(node => node.NodeKey == key);
        if (index < 0) return;
        _selectedNodeKey = key;
        _selectedEdge = null;
        foreach (var item in _edgeVisuals)
            item.Visible.SetResourceReference(Shape.StrokeProperty, "PrimaryBrush");
        foreach (var nodeCard in _canvas.Children.OfType<Border>().Where(item => item.Tag is string))
            nodeCard.SetResourceReference(Border.BorderBrushProperty,
                ReferenceEquals(nodeCard, card) ? "ActionBrush" : "EdgeBrush");
        if (e.ClickCount == 2)
        {
            if (_editable) EditCanvasNode(key);
            e.Handled = true;
            return;
        }
        if (!_editable) return;
        _draggedNode = card;
        _nodeMoved = false;
        var cursor = e.GetPosition(_canvas);
        var point = PositionOf(key, index);
        _dragOffset = new Point(cursor.X - point.X, cursor.Y - point.Y);
        card.CaptureMouse();
        e.Handled = true;
    }

    private void CanvasNodeMove(object sender, MouseEventArgs e)
    {
        if (_draggedNode is null || !ReferenceEquals(sender, _draggedNode) ||
            e.LeftButton != MouseButtonState.Pressed || _draggedNode.Tag is not string key) return;
        var cursor = e.GetPosition(_canvas);
        var point = new Point(Math.Max(14, cursor.X - _dragOffset.X),
            Math.Max(14, cursor.Y - _dragOffset.Y));
        var previous = _positions[key];
        if (Math.Abs(point.X - previous.X) < 2 && Math.Abs(point.Y - previous.Y) < 2) return;
        _nodeMoved = true;
        _positions[key] = point;
        Canvas.SetLeft(_draggedNode, point.X);
        Canvas.SetTop(_draggedNode, point.Y);
        PositionPorts(key, _definition.Nodes.FindIndex(node => node.NodeKey == key));
        UpdatePortVisibility(cursor);
        // 连线随节点位置实时更新。
        foreach (var (edge, visible, hitArea) in _edgeVisuals)
        {
            var fromIndex = _definition.Nodes.FindIndex(node => node.NodeKey == edge.FromNodeKey);
            var toIndex = _definition.Nodes.FindIndex(node => node.NodeKey == edge.ToNodeKey);
            var ports = PortsOf(edge);
            var from = PortPoint(edge.FromNodeKey, fromIndex, ports.Source);
            var to = PortPoint(edge.ToNodeKey, toIndex, ports.Target);
            var geometry = EdgeGeometry(from, to, ports);
            visible.Data = geometry;
            hitArea.Data = geometry;
        }
        _canvas.Width = Math.Max(_canvas.Width, point.X + 180);
        _canvas.Height = Math.Max(_canvas.Height, point.Y + 100);
    }

    private void CanvasNodeUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedNode is null) return;
        _draggedNode.ReleaseMouseCapture();
        _draggedNode = null;
        if (_nodeMoved) RenderCanvas();
        if (_nodeMoved) MarkDirty();
        _nodeMoved = false;
        e.Handled = true;
    }

    private void CanvasPortDown(object sender, MouseButtonEventArgs e)
    {
        if (!_editable || sender is not Grid { Tag: FlowPort port }) return;
        var index = _definition.Nodes.FindIndex(node => node.NodeKey == port.NodeKey);
        if (index < 0) return;
        if (_definition.Nodes[index].NodeType == "End")
        {
            _status.Text = "结束节点不能引出连线。";
            e.Handled = true;
            return;
        }
        _connectingFrom = port;
        var from = PortPoint(port.NodeKey, index, port.Side);
        var pointer = e.GetPosition(_canvas);
        _connectionPreview = new Line
        {
            X1 = from.X, Y1 = from.Y,
            X2 = pointer.X, Y2 = pointer.Y,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([5, 3]),
            IsHitTestVisible = false
        };
        _connectionPreview.SetResourceReference(Shape.StrokeProperty, "ActionBrush");
        _canvas.Children.Add(_connectionPreview);
        Mouse.Capture(_canvas);
        e.Handled = true;
    }

    private void CanvasConnectionMove(object sender, MouseEventArgs e)
    {
        UpdatePortVisibility(e.GetPosition(_canvas));
        if (_connectionPreview is null) return;
        var pointer = e.GetPosition(_canvas);
        _connectionPreview.X2 = pointer.X;
        _connectionPreview.Y2 = pointer.Y;
    }

    private void CanvasConnectionUp(object sender, MouseButtonEventArgs e)
    {
        if (_connectingFrom is null) return;
        var sourcePort = _connectingFrom;
        var targetPort = HitPort(e.GetPosition(_canvas));
        _connectingFrom = null;
        if (_connectionPreview is not null) _canvas.Children.Remove(_connectionPreview);
        _connectionPreview = null;
        Mouse.Capture(null);
        if (targetPort is null) return;
        var from = sourcePort.NodeKey;
        var target = targetPort.NodeKey;
        var sourceNode = _definition.Nodes.First(node => node.NodeKey == from);
        var targetNode = _definition.Nodes.First(node => node.NodeKey == target);
        if (from == target || targetNode.NodeType == "Start" || sourceNode.NodeType == "End" ||
            _definition.Edges.Any(edge => edge.FromNodeKey == from && edge.ToNodeKey == target))
        {
            _status.Text = "连线无效：请选择不同节点，且不能重复连接或连接到开始节点。";
            return;
        }
        if (sourceNode.NodeType is "Start" or "Cc" &&
            _definition.Edges.Count(edge => edge.FromNodeKey == from) >= 1)
        {
            _status.Text = "开始节点和抄送节点最多只有一条出边。";
            return;
        }
        var edge = new ApprovalFlowEdge { FromNodeKey = from, ToNodeKey = target };
        _definition.Edges.Add(edge);
        _edgePorts[edge] = new EdgePorts(sourcePort.Side, targetPort.Side, LineStyle.Straight);
        _selectedEdge = edge;
        _selectedNodeKey = null;
        RenderCanvas();
        MarkDirty();
        _status.Text = $"已连接 {from} → {target}；双击连线可设置条件，保存后生效。";
        e.Handled = true;
    }

    private FlowPort? HitPort(Point point)
    {
        var hit = VisualTreeHelper.HitTest(_canvas, point)?.VisualHit;
        while (hit is not null && !ReferenceEquals(hit, _canvas))
        {
            if (hit is FrameworkElement { Tag: FlowPort port }) return port;
            if (hit is FrameworkElement { Tag: string key })
            {
                var index = _definition.Nodes.FindIndex(node => node.NodeKey == key);
                if (index >= 0)
                {
                    var node = PositionOf(key, index);
                    var offsetX = point.X - node.X - 75;
                    var offsetY = point.Y - node.Y - 34;
                    var side = Math.Abs(offsetX / 75) > Math.Abs(offsetY / 34)
                        ? offsetX >= 0 ? PortSide.Right : PortSide.Left
                        : offsetY >= 0 ? PortSide.Bottom : PortSide.Top;
                    return new FlowPort(key, side);
                }
            }
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    private void CanvasEdgeDown(object sender, MouseButtonEventArgs e)
    {
        var visual = _edgeVisuals.FirstOrDefault(item => ReferenceEquals(item.HitArea, sender));
        if (visual.Edge is null) return;
        _selectedEdge = visual.Edge;
        _selectedNodeKey = null;
        foreach (var item in _edgeVisuals)
            item.Visible.SetResourceReference(Shape.StrokeProperty,
                ReferenceEquals(item.Edge, _selectedEdge) ? "ActionBrush" : "PrimaryBrush");
        if (e.ClickCount == 2 && _editable) EditCanvasEdge(visual.Edge);
        e.Handled = true;
    }

    private void EditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _connectingFrom is not null)
        {
            _connectingFrom = null;
            if (_connectionPreview is not null) _canvas.Children.Remove(_connectionPreview);
            _connectionPreview = null;
            Mouse.Capture(null);
            e.Handled = true;
            return;
        }
        if (!_editable || e.Key != Key.Delete || Keyboard.FocusedElement is TextBox) return;
        if (_selectedEdge is not null)
        {
            DeleteCanvasEdge(_selectedEdge);
            e.Handled = true;
        }
        else if (_selectedNodeKey is not null)
        {
            DeleteNode(_selectedNodeKey);
            e.Handled = true;
        }
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<Guid> ParseIds(string value) => string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Guid.Parse).Distinct().ToList();

    private static void ValidateJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        using var _ = JsonDocument.Parse(value);
    }
}
