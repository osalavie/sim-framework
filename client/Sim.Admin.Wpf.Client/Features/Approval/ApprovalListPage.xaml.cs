using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.Approval;

public enum ApprovalListKind
{
    Pending,
    Handled,
    Submitted,
    Cc,
    Definitions,
    Monitor
}

public partial class ApprovalListPage : UserControl
{
    private readonly ApiClient _api;
    private readonly ApprovalListKind _kind;
    private readonly Action _sessionExpired;
    private CustomButton? _handleButton;
    private CustomButton? _viewButton;
    private CustomButton? _markReadButton;
    private CustomButton? _terminateButton;
    private CustomButton? _publishButton;
    private CustomButton? _disableButton;
    private CustomButton? _versionButton;
    private CustomButton? _deleteButton;
    private CustomButton? _createButton;
    private CustomButton? _validateButton;
    private CustomButton? _editDefinitionButton;
    private int _page;
    private int _total;
    private bool _busy;
    private bool _loaded;

    public ApprovalListPage(ApiClient api, ApprovalListKind kind, Action sessionExpired)
    {
        _api = api;
        _kind = kind;
        _sessionExpired = sessionExpired;
        InitializeComponent();
        BuildColumns();
        BuildActions();
        ConfigureFilters();
        Pager.PageChanged += async (_, _) =>
        {
            _page = Math.Max(0, Pager.PageIndex - 1);
            await LoadAsync();
        };
        Pager.PageSizeChanged += async (_, _) =>
        {
            _page = 0;
            await LoadAsync();
        };
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await LoadAsync();
        };
        UpdateButtons();
    }

    private void ConfigureFilters()
    {
        if (_kind == ApprovalListKind.Definitions)
        {
            DefinitionFilter.Visibility = Visibility.Collapsed;
            Pager.Visibility = Visibility.Collapsed;
        }
        else if (_kind == ApprovalListKind.Cc)
        {
            DefinitionFilter.Visibility = Visibility.Collapsed;
        }
    }

    private void BuildActions()
    {
        switch (_kind)
        {
            case ApprovalListKind.Pending:
                _handleButton = AddAction("处理", primary: true);
                _handleButton.Click += HandleClick;
                _viewButton = AddAction("查看");
                break;
            case ApprovalListKind.Handled:
            case ApprovalListKind.Submitted:
                _viewButton = AddAction("查看");
                break;
            case ApprovalListKind.Cc:
                _markReadButton = AddAction("标记已读");
                _markReadButton.Click += MarkReadClick;
                _viewButton = AddAction("查看");
                break;
            case ApprovalListKind.Monitor:
                _viewButton = AddAction("查看");
                _terminateButton = AddAction("终止", primary: true);
                _terminateButton.Click += TerminateClick;
                break;
            case ApprovalListKind.Definitions:
                _createButton = AddAction("新建流程", primary: true);
                _createButton.Click += CreateDefinitionClick;
                _editDefinitionButton = AddAction("查看/编辑");
                _editDefinitionButton.Click += EditDefinitionClick;
                _validateButton = AddAction("校验");
                _validateButton.Click += ValidateDefinitionClick;
                _publishButton = AddAction("发布", primary: true);
                _publishButton.Click += PublishClick;
                _disableButton = AddAction("停用");
                _disableButton.Click += DisableClick;
                _versionButton = AddAction("升版");
                _versionButton.Click += NewVersionClick;
                _deleteButton = AddAction("删除");
                _deleteButton.Click += DeleteDefinitionClick;
                break;
        }
        if (_viewButton is not null) _viewButton.Click += ViewClick;
    }

    private CustomButton AddAction(string text, bool primary = false)
    {
        var button = new CustomButton
        {
            Content = text,
            Height = 34,
            Margin = new Thickness(8, 0, 0, 0),
            Variant = primary ? CustomButtonVariant.Primary : CustomButtonVariant.Secondary
        };
        ActionPanel.Children.Add(button);
        return button;
    }

    private void BuildColumns()
    {
        switch (_kind)
        {
            case ApprovalListKind.Pending:
                AddColumn("标题", nameof(ApprovalTask.InstanceTitle), 0, star: true);
                AddColumn("编号", nameof(ApprovalTask.InstanceNumber), 150);
                AddColumn("业务类型", nameof(ApprovalTask.BusinessType), 130);
                AddColumn("节点", nameof(ApprovalTask.NodeName), 130);
                AddColumn("到达时间", nameof(ApprovalTask.CreationTime), 140, format: "yyyy-MM-dd HH:mm");
                AddColumn("截止时间", nameof(ApprovalTask.DueAt), 140, format: "yyyy-MM-dd HH:mm");
                AddColumn("状态", nameof(ApprovalTask.InstanceStatus), 100, converter: "ApprovalStatus");
                break;
            case ApprovalListKind.Handled:
                AddColumn("标题", nameof(ApprovalTask.InstanceTitle), 0, star: true);
                AddColumn("编号", nameof(ApprovalTask.InstanceNumber), 150);
                AddColumn("节点", nameof(ApprovalTask.NodeName), 130);
                AddColumn("处理结果", nameof(ApprovalTask.Status), 100, converter: "ApprovalStatus");
                AddColumn("处理时间", nameof(ApprovalTask.HandledTime), 140, format: "yyyy-MM-dd HH:mm");
                AddColumn("实例状态", nameof(ApprovalTask.InstanceStatus), 100, converter: "ApprovalStatus");
                AddColumn("意见", nameof(ApprovalTask.Comment), 180);
                break;
            case ApprovalListKind.Submitted:
                AddColumn("标题", nameof(ApprovalInstanceSummary.Title), 0, star: true);
                AddColumn("编号", nameof(ApprovalInstanceSummary.Number), 150);
                AddColumn("流程", nameof(ApprovalInstanceSummary.DefinitionName), 160);
                AddColumn("业务类型", nameof(ApprovalInstanceSummary.BusinessType), 120);
                AddColumn("当前节点", nameof(ApprovalInstanceSummary.CurrentNodeName), 130);
                AddColumn("状态", nameof(ApprovalInstanceSummary.Status), 100, converter: "ApprovalStatus");
                AddColumn("创建时间", nameof(ApprovalInstanceSummary.CreationTime), 140, format: "yyyy-MM-dd HH:mm");
                break;
            case ApprovalListKind.Cc:
                AddColumn("标题", nameof(ApprovalCc.InstanceTitle), 0, star: true);
                AddColumn("编号", nameof(ApprovalCc.InstanceNumber), 160);
                AddColumn("状态", nameof(ApprovalCc.InstanceStatus), 100, converter: "ApprovalStatus");
                AddColumn("收到时间", nameof(ApprovalCc.CreationTime), 140, format: "yyyy-MM-dd HH:mm");
                AddColumn("已读时间", nameof(ApprovalCc.ReadAt), 140, format: "yyyy-MM-dd HH:mm");
                AddColumn("阅读状态", nameof(ApprovalCc.ReadState), 80);
                break;
            case ApprovalListKind.Monitor:
                AddColumn("标题", nameof(ApprovalInstanceSummary.Title), 0, star: true);
                AddColumn("编号", nameof(ApprovalInstanceSummary.Number), 150);
                AddColumn("业务类型", nameof(ApprovalInstanceSummary.BusinessType), 120);
                AddColumn("当前节点", nameof(ApprovalInstanceSummary.CurrentNodeName), 130);
                AddColumn("状态", nameof(ApprovalInstanceSummary.Status), 100, converter: "ApprovalStatus");
                AddColumn("创建时间", nameof(ApprovalInstanceSummary.CreationTime), 140, format: "yyyy-MM-dd HH:mm");
                break;
            case ApprovalListKind.Definitions:
                AddColumn("编码", nameof(ApprovalFlowDefinition.Code), 140);
                AddColumn("名称", nameof(ApprovalFlowDefinition.Name), 0, star: true);
                AddColumn("版本", nameof(ApprovalFlowDefinition.Version), 60);
                AddColumn("业务类型", nameof(ApprovalFlowDefinition.BusinessType), 130);
                AddColumn("节点数", nameof(ApprovalFlowDefinition.NodeCount), 70);
                AddColumn("状态", nameof(ApprovalFlowDefinition.Status), 100, converter: "ApprovalStatus");
                AddColumn("发布时间", nameof(ApprovalFlowDefinition.PublishedTime), 150, format: "yyyy-MM-dd HH:mm");
                break;
        }
    }

    private void AddColumn(string header, string path, double width, bool star = false,
        string? converter = null, string? format = null)
    {
        var binding = new Binding(path);
        if (format is not null) binding.StringFormat = format;
        if (converter is not null) binding.Converter = (IValueConverter)Application.Current.FindResource(converter);
        Records.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Binding = binding,
            Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(width),
            ElementStyle = EllipsisStyle
        });
    }

    private static readonly Style EllipsisStyle = CreateEllipsisStyle();

    private static Style CreateEllipsisStyle()
    {
        var style = new Style(typeof(TextBlock), (Style?)Application.Current.TryFindResource(typeof(TextBlock)));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        return style;
    }

    private async Task LoadCoreAsync()
    {
        var keyword = SearchBox.Text.Trim();
        var definitionCode = DefinitionFilter.Text.Trim();
        var pageSize = Math.Max(1, Pager.PageSize);
        var skipCount = _page * pageSize;
        switch (_kind)
        {
            case ApprovalListKind.Pending:
            {
                var page = await _api.GetApprovalPendingAsync(skipCount, pageSize, keyword, definitionCode);
                Records.ItemsSource = page.Items;
                _total = page.TotalCount;
                break;
            }
            case ApprovalListKind.Handled:
            {
                var page = await _api.GetApprovalHandledAsync(skipCount, pageSize, keyword, definitionCode);
                Records.ItemsSource = page.Items;
                _total = page.TotalCount;
                break;
            }
            case ApprovalListKind.Submitted:
            {
                var page = await _api.GetApprovalSubmittedAsync(skipCount, pageSize, keyword, definitionCode);
                Records.ItemsSource = page.Items;
                _total = page.TotalCount;
                break;
            }
            case ApprovalListKind.Cc:
            {
                var page = await _api.GetApprovalCcAsync(skipCount, pageSize, keyword);
                Records.ItemsSource = page.Items;
                _total = page.TotalCount;
                break;
            }
            case ApprovalListKind.Monitor:
            {
                var page = await _api.GetApprovalMonitorAsync(skipCount, pageSize, keyword,
                    definitionCode: definitionCode);
                Records.ItemsSource = page.Items;
                _total = page.TotalCount;
                break;
            }
            case ApprovalListKind.Definitions:
            {
                var definitions = await _api.GetApprovalDefinitionsAsync();
                var filtered = string.IsNullOrWhiteSpace(keyword) ? definitions : definitions.Where(item =>
                    (item.Code?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (item.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (item.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
                Records.ItemsSource = filtered;
                _total = filtered.Count;
                break;
            }
        }
        Status.Text = _kind == ApprovalListKind.Definitions
            ? $"共 {_total} 个流程定义"
            : $"第 {_page + 1} 页 · 共 {_total} 条";
        Pager.PageCount = Math.Max(1, (_total + pageSize - 1) / pageSize);
        Pager.PageIndex = _page + 1;
        UpdateButtons();
    }

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async void HandleClick(object sender, RoutedEventArgs e)
    {
        if (Records.SelectedItem is not ApprovalTask task) return;
        var request = ApprovalActionDialog.Show(Window.GetWindow(this), _api, task);
        if (request is null) return;
        await RunAsync(async () =>
        {
            await ExecuteActionAsync(task, request);
            await LoadCoreAsync();
            Status.Text = "待办已处理";
        });
    }

    private async void MarkReadClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (Records.SelectedItem is not ApprovalCc record)
            throw new InvalidOperationException("请先选择一条抄送记录。");
        await _api.MarkApprovalCcReadAsync(record.Id);
        await LoadCoreAsync();
        Status.Text = "抄送已标记为已读";
    });

    private async void TerminateClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (Records.SelectedItem is not ApprovalInstanceSummary instance)
            throw new InvalidOperationException("请先选择一个审批实例。");
        var values = CustomPromptDialog.Show(Window.GetWindow(this), "终止流程",
            "终止后流程立即结束且不可恢复，请填写终止原因。",
            [new CustomPromptField("终止原因", Multiline: true)], "终止流程", danger: true);
        if (values is null) return;
        await _api.TerminateApprovalAsync(instance.Id, values[0].Trim());
        await LoadCoreAsync();
        Status.Text = "流程已终止";
    });

    private async void PublishClick(object sender, RoutedEventArgs e) => await DefinitionActionAsync(
        "发布", definition => _api.PublishApprovalDefinitionAsync(definition.Id));

    private async void CreateDefinitionClick(object sender, RoutedEventArgs e)
    {
        var values = CustomPromptDialog.Show(Window.GetWindow(this), "新建流程",
            "创建后生成“开始 → 发起人审批 → 结束”的草稿流程。",
            [new CustomPromptField("流程编码"), new CustomPromptField("流程名称"),
                new CustomPromptField("业务类型", Optional: true),
                new CustomPromptField("说明", Multiline: true, Optional: true)], "创建");
        if (values is null) return;
        await RunAsync(async () =>
        {
            var created = await _api.CreateApprovalDefinitionAsync(values[0].Trim(), values[1].Trim(),
                string.IsNullOrWhiteSpace(values[2]) ? null : values[2].Trim(),
                string.IsNullOrWhiteSpace(values[3]) ? null : values[3].Trim());
            await LoadCoreAsync();
            Status.Text = $"流程草稿“{created.Name}”已创建，可校验并发布。";
            await ShowDefinitionEditorAsync(created.Id);
        });
    }

    private async void EditDefinitionClick(object sender, RoutedEventArgs e)
    {
        if (Records.SelectedItem is ApprovalFlowDefinition definition)
            await RunAsync(() => ShowDefinitionEditorAsync(definition.Id));
    }

    private async Task ShowDefinitionEditorAsync(Guid id)
    {
        var detail = await _api.GetApprovalDefinitionAsync(id);
        var editor = new ApprovalFlowEditorWindow(_api, detail, _sessionExpired)
        {
            Owner = Window.GetWindow(this)
        };
        editor.ShowDialog();
        if (editor.Changed) await LoadCoreAsync();
    }

    private async void ValidateDefinitionClick(object sender, RoutedEventArgs e)
    {
        if (Records.SelectedItem is not ApprovalFlowDefinition definition) return;
        await RunAsync(async () =>
        {
            var result = await _api.ValidateApprovalDefinitionAsync(definition.Id);
            Status.Text = result.Valid ? "流程校验通过。" : string.Join("；", result.Problems);
        });
    }

    private async void DisableClick(object sender, RoutedEventArgs e) => await DefinitionActionAsync(
        "停用", definition => _api.DisableApprovalDefinitionAsync(definition.Id));

    private async void NewVersionClick(object sender, RoutedEventArgs e) => await DefinitionActionAsync(
        "升版", definition => _api.NewApprovalDefinitionVersionAsync(definition.Id));

    private async void DeleteDefinitionClick(object sender, RoutedEventArgs e) => await DefinitionActionAsync(
        "删除", definition => _api.DeleteApprovalDefinitionAsync(definition.Id), danger: true);

    private async Task DefinitionActionAsync(string action, Func<ApprovalFlowDefinition, Task> execute,
        bool danger = false)
    {
        if (Records.SelectedItem is not ApprovalFlowDefinition definition)
        {
            Status.Text = "请先选择一个流程定义。";
            return;
        }
        if (MessageBox.Show(Window.GetWindow(this),
                $"确定{action}流程“{definition.Name ?? definition.Code}”吗？", action,
                MessageBoxButton.YesNo, danger ? MessageBoxImage.Warning : MessageBoxImage.Question) !=
            MessageBoxResult.Yes)
            return;
        await RunAsync(async () =>
        {
            await execute(definition);
            await LoadCoreAsync();
            Status.Text = $"流程已{action}";
        });
    }

    private void ViewClick(object sender, RoutedEventArgs e) => OpenSelectedDetail();

    private async void RecordsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_kind == ApprovalListKind.Definitions && Records.SelectedItem is ApprovalFlowDefinition definition)
            await RunAsync(() => ShowDefinitionEditorAsync(definition.Id));
        else OpenSelectedDetail();
    }

    private void OpenSelectedDetail()
    {
        var instanceId = Records.SelectedItem switch
        {
            ApprovalTask task => task.InstanceId,
            ApprovalInstanceSummary summary => summary.Id,
            ApprovalCc cc => cc.InstanceId,
            _ => Guid.Empty
        };
        if (instanceId == Guid.Empty) return;
        var window = new ApprovalDetailWindow(_api, instanceId, _kind == ApprovalListKind.Monitor,
            _sessionExpired)
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
        if (window.Changed) _ = LoadAsync();
    }

    private async Task ExecuteActionAsync(ApprovalTask task, ApprovalActionRequest request)
    {
        switch (request.Kind)
        {
            case ApprovalTaskActionKind.Approve:
                await _api.ApproveApprovalTaskAsync(task.Id, request.Comment);
                break;
            case ApprovalTaskActionKind.Reject:
                await _api.RejectApprovalTaskAsync(task.Id, request.Comment);
                break;
            case ApprovalTaskActionKind.ReturnToPrevious:
                await _api.ReturnApprovalToPreviousAsync(task.Id, request.Comment);
                break;
            case ApprovalTaskActionKind.ReturnToInitiator:
                await _api.ReturnApprovalToInitiatorAsync(task.Id, request.Comment);
                break;
            case ApprovalTaskActionKind.Transfer:
                await _api.TransferApprovalTaskAsync(task.Id, request.TargetUserId
                    ?? throw new InvalidOperationException("请选择转交目标。"), request.Comment);
                break;
            case ApprovalTaskActionKind.AddSign:
                await _api.AddSignApprovalTaskAsync(task.Id, request.TargetUserId
                    ?? throw new InvalidOperationException("请选择加签目标。"), request.Position, request.Comment);
                break;
        }
    }

    private async void SearchClick(object sender, RoutedEventArgs e)
    {
        _page = 0;
        await LoadAsync();
    }

    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _page = 0;
        _ = LoadAsync();
    }

    private async void RefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        if (Pager is null) return;
        var selected = Records.SelectedItem;
        Enable(_handleButton, !_busy && selected is ApprovalTask);
        Enable(_viewButton, !_busy && selected is ApprovalTask or ApprovalInstanceSummary or ApprovalCc);
        Enable(_markReadButton, !_busy && selected is ApprovalCc { ReadAt: null });
        Enable(_terminateButton, !_busy && selected is ApprovalInstanceSummary);
        Enable(_publishButton, !_busy && selected is ApprovalFlowDefinition);
        Enable(_createButton, !_busy);
        Enable(_editDefinitionButton, !_busy && selected is ApprovalFlowDefinition);
        Enable(_validateButton, !_busy && selected is ApprovalFlowDefinition);
        Enable(_disableButton, !_busy && selected is ApprovalFlowDefinition);
        Enable(_versionButton, !_busy && selected is ApprovalFlowDefinition);
        Enable(_deleteButton, !_busy && selected is ApprovalFlowDefinition);
        Pager.IsEnabled = !_busy && _kind != ApprovalListKind.Definitions;
    }

    private static void Enable(CustomButton? button, bool enabled)
    {
        if (button is not null) button.IsEnabled = enabled;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        try { await action(); }
        catch (SessionExpiredException) { _sessionExpired(); }
        catch (Exception error)
        {
            Status.Text = error is TaskCanceledException ? "连接超时，请稍后重试。" : error.Message;
        }
        finally
        {
            _busy = false;
            UpdateButtons();
        }
    }
}
