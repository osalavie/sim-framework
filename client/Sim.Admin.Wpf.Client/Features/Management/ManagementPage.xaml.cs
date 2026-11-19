using System.ComponentModel;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Controls.Inputs;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.Management;

public partial class ManagementPage : UserControl
{
    private readonly ApiClient _api;
    private readonly ManagementDefinition _definition;
    private readonly Action? _sessionExpired;
    private List<ManagementRow> _rows = [];
    private List<ManagementRow> _visibleRows = [];
    private readonly Dictionary<DataGridColumn, string> _columnKeys = [];
    private string? _sortKey;
    private bool _sortAscending;
    private DataGridColumn? _sortColumn;
    private int _page;
    private int _total;
    private bool _busy;
    private bool _loaded;

    public ManagementPage(ApiClient api, ManagementDefinition definition, Action? sessionExpired = null)
    {
        _api = api;
        _definition = definition;
        _sessionExpired = sessionExpired;
        InitializeComponent();
        CreateButton.Visibility = definition.CanCreate ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Visibility = definition.CanDelete ? Visibility.Visible : Visibility.Collapsed;
        Pager.Visibility = definition.Paged ? Visibility.Visible : Visibility.Collapsed;
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
        foreach (var action in definition.Actions ?? [])
        {
            var button = new CustomButton
            {
                Content = action.Label,
                Height = 34,
                Margin = new Thickness(8, 0, 0, 0),
                Variant = action.Danger ? CustomButtonVariant.Danger : CustomButtonVariant.Secondary
            };
            button.Click += async (_, _) => await ExecuteActionAsync(action);
            RowActions.Children.Add(button);
        }

        Records.CheckedChanged += (_, _) => UpdateButtons();

        var isFirstColumn = true;
        foreach (var field in definition.Fields.Where(x => x.ShowInTable))
        {
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty,
                isFirstColumn ? FontWeights.SemiBold : FontWeights.Normal));
            textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            var column = new DataGridTextColumn
            {
                Header = field.Label,
                Binding = new Binding($"Values[{field.Key}]") { FallbackValue = "" },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                ElementStyle = textStyle,
                HeaderStyle = (Style)FindResource("CustomDataGridColumnHeaderStyle")
            };
            _columnKeys[column] = field.Key;
            Records.Columns.Add(column);
            isFirstColumn = false;
        }

        if (definition.CanUpdate || definition.CanDelete ||
            definition.Actions is { Count: > 0 } || definition.Relations is { Count: > 0 })
        {
            var actionColumn = new DataGridTemplateColumn
            {
                Header = "操作",
                CellTemplate = (DataTemplate)FindResource("RowActionsTemplate"),
                HeaderStyle = (Style)FindResource("CustomDataGridColumnHeaderStyle"),
                // 宽度随该页最宽一行的操作按钮组自适应。
                Width = DataGridLength.Auto,
                CanUserSort = false,
                CanUserResize = !definition.ActionsFixed
            };
            if (definition.ActionsFixed) Records.FrozenRightColumn = actionColumn;
            else Records.Columns.Add(actionColumn);
        }
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await LoadAsync();
        };
        UpdateButtons();
    }

    private async Task LoadAsync()
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        try { await LoadCoreAsync(); }
        catch (Exception error)
        {
            Records.ItemsSource = null;
            if (error is SessionExpiredException && _sessionExpired is not null) _sessionExpired();
            else Status.Text = $"加载{_definition.Title}失败：{error.Message}";
        }
        finally { _busy = false; UpdateButtons(); }
    }

    private async Task LoadCoreAsync()
    {
        var path = _definition.Path;
        var pageSize = Math.Max(1, Pager.PageSize);
        if (_definition.Paged) path += $"?skipCount={_page * pageSize}&maxResultCount={pageSize}";
        var document = await _api.GetDocumentAsync(path);
        var list = _definition.ItemsProperty is { } property
            ? document.GetProperty(property)
            : document.ValueKind == JsonValueKind.Array ? document : document.GetProperty("items");
        if (list.ValueKind == JsonValueKind.Null)
        {
            _rows = [];
            _total = 0;
            ApplyFilter();
            return;
        }
        _rows = list.EnumerateArray().Select(item => new ManagementRow(item, _definition.Fields)).ToList();
        foreach (var row in _rows) row.Actions = BuildRowActions(row);
        _total = document.ValueKind == JsonValueKind.Array
            ? _rows.Count
            : document.TryGetProperty("totalCount", out var total) ? total.GetInt32() : _rows.Count;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var keyword = SearchBox.Text.Trim();
        var visible = keyword.Length == 0
            ? _rows
            : _rows.Where(row => row.Values.Values.Any(value =>
                value.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
        if (_sortKey is not null)
        {
            var comparer = ValueComparer.Instance;
            visible = (_sortAscending
                    ? visible.OrderBy(row => SortValue(row, _sortKey), comparer)
                    : visible.OrderByDescending(row => SortValue(row, _sortKey), comparer))
                .ToList();
        }
        _visibleRows = visible;
        Records.ItemsSource = _visibleRows;
        if (_sortColumn is not null)
            _sortColumn.SortDirection = _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        Status.Text = _definition.Paged
            ? $"第 {_page + 1} 页 · 共 {_total} 条 · 本页 {_visibleRows.Count} 条（搜索筛选当前页）"
            : $"共 {_total} 条 · 显示 {_visibleRows.Count} 条";
        var pageSize = Math.Max(1, Pager.PageSize);
        Pager.PageCount = Math.Max(1, (_total + pageSize - 1) / pageSize);
        Pager.PageIndex = _page + 1;
        UpdateButtons();
    }

    private void RecordsSorting(object sender, DataGridSortingEventArgs e)
    {
        if (!_columnKeys.TryGetValue(e.Column, out var key)) return;
        e.Handled = true;
        if (_sortKey == key) _sortAscending = !_sortAscending;
        else
        {
            _sortKey = key;
            _sortAscending = true;
        }
        foreach (var column in Records.Columns) column.SortDirection = null;
        _sortColumn = e.Column;
        e.Column.SortDirection = _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        ApplyFilter();
    }

    private static string SortValue(ManagementRow row, string key) =>
        row.Values.TryGetValue(key, out var value) ? value : string.Empty;

    private sealed class ValueComparer : IComparer<string>
    {
        public static readonly ValueComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var left = x ?? string.Empty;
            var right = y ?? string.Empty;
            if (double.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftNumber) &&
                double.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightNumber))
                return leftNumber.CompareTo(rightNumber);
            if (DateTime.TryParse(left, CultureInfo.InvariantCulture, DateTimeStyles.None, out var leftDate) &&
                DateTime.TryParse(right, CultureInfo.InvariantCulture, DateTimeStyles.None, out var rightDate))
                return leftDate.CompareTo(rightDate);
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private IReadOnlyList<ManagementRowAction> BuildRowActions(ManagementRow row)
    {
        var actions = new List<ManagementRowAction>();
        if (_definition.CanUpdate) actions.Add(new() { Label = "编辑", Kind = "edit", Row = row });
        foreach (var action in _definition.Actions ?? [])
            actions.Add(new() { Label = action.Label, Kind = "action", Action = action, Row = row });
        if (!row.IsBuiltIn)
        {
            foreach (var relation in _definition.Relations ?? [])
                actions.Add(new()
                {
                    Label = relation.Label, Kind = "relation", Relation = relation, Row = row
                });
        }
        if (_definition.CanDelete && !row.IsBuiltIn)
            actions.Add(new() { Label = "删除", Kind = "delete", Row = row, Danger = true });
        return actions;
    }

    private async void RowActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CustomButton { Tag: ManagementRowAction action }) return;
        switch (action.Kind)
        {
            case "edit":
                await EditRowAsync(action.Row);
                break;
            case "delete":
                if (!Confirm($"确定删除“{action.Row.Values.Values.FirstOrDefault() ?? action.Row.Id}”吗？")) return;
                await DeleteRowsAsync([action.Row]);
                break;
            case "action" when action.Action is not null:
                await ExecuteActionAsync(action.Action, action.Row);
                break;
            case "relation" when action.Relation is not null:
                await ManageRelationAsync(action.Relation, action.Row);
                break;
        }
    }

    /// <summary>工具栏只处理复选框勾选的记录，光标选中的单行不参与批量操作。</summary>
    private List<ManagementRow> TargetRows() =>
        Records.CheckedItems.Cast<ManagementRow>().ToList();

    private async Task MutateAsync(HttpMethod method, string path, JsonObject? body = null)
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        try
        {
            await _api.SendDocumentAsync(method, path, body);
            _busy = false;
            await LoadAsync();
        }
        catch (Exception error)
        {
            if (error is SessionExpiredException && _sessionExpired is not null) _sessionExpired();
            else Status.Text = $"操作失败：{error.Message}";
        }
        finally { _busy = false; UpdateButtons(); }
    }

    private async void CreateClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ManagementEditor(_definition) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.Result is not null)
            await MutateAsync(ToHttpMethod(_definition.CreateMethod),
                _definition.CreatePath ?? _definition.ItemBasePath ?? _definition.Path, dialog.Result);
    }

    private async Task EditRowAsync(ManagementRow row)
    {
        var dialog = new ManagementEditor(_definition, row.Raw) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        var basePath = _definition.ItemBasePath ?? _definition.Path;
        var path = _definition.UpdatePath ?? (_definition.UpdateIdInBody ? _definition.Path : $"{basePath}/{row.Id}");
        if (_definition.UpdateIdInBody) dialog.Result["id"] = row.Id;
        await MutateAsync(ToHttpMethod(_definition.UpdateMethod), path, dialog.Result);
    }

    private async void DeleteClick(object sender, RoutedEventArgs e)
    {
        var rows = TargetRows();
        if (rows.Count == 0) { Status.Text = "请先勾选要删除的记录。"; return; }
        if (rows.Any(row => row.IsBuiltIn)) { Status.Text = $"内置{_definition.Title}不可删除。"; return; }
        var message = rows.Count == 1
            ? $"确定删除“{rows[0].Values.Values.FirstOrDefault() ?? rows[0].Id}”吗？"
            : $"确定删除选中的 {rows.Count} 条记录吗？";
        if (MessageBox.Show(Window.GetWindow(this), message, "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await DeleteRowsAsync(rows);
    }

    private async Task DeleteRowsAsync(IReadOnlyList<ManagementRow> rows)
    {
        if (_busy || rows.Count == 0) return;
        _busy = true;
        UpdateButtons();
        try
        {
            foreach (var row in rows)
            {
                var path = _definition.DeletePath
                    ?? $"{_definition.ItemBasePath ?? _definition.Path}/{row.Id}";
                await _api.SendDocumentAsync(ToHttpMethod(_definition.DeleteMethod), path);
            }
            _busy = false;
            await LoadAsync();
        }
        catch (Exception error)
        {
            if (error is SessionExpiredException && _sessionExpired is not null) _sessionExpired();
            else Status.Text = $"操作失败：{error.Message}";
        }
        finally { _busy = false; UpdateButtons(); }
    }

    private static HttpMethod ToHttpMethod(ManagementHttpMethod method) => method switch
    {
        ManagementHttpMethod.Post => HttpMethod.Post,
        ManagementHttpMethod.Delete => HttpMethod.Delete,
        _ => HttpMethod.Put
    };

    private async Task ExecuteActionAsync(ManagementAction action)
    {
        var row = TargetRows().FirstOrDefault();
        if (row is null)
        {
            Status.Text = "请先选择一条记录。";
            return;
        }
        await ExecuteActionAsync(action, row);
    }

    private async Task ExecuteActionAsync(ManagementAction action, ManagementRow row)
    {
        if (!Guid.TryParse(row.Id, out var id))
        {
            Status.Text = "该记录缺少有效主键，无法执行此操作。";
            return;
        }
        var title = row.Values.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? row.Id;
        switch (action.Kind)
        {
            case ManagementActionKind.ResetPassword:
                if (!Confirm($"确定将“{title}”的密码重置为系统默认密码吗？")) return;
                await RunActionAsync(async () =>
                {
                    await _api.ResetUserPasswordAsync(id);
                    await LoadCoreAsync();
                }, "密码已重置为系统默认密码");
                break;
            case ManagementActionKind.AssignRoles:
                await AssignRolesAsync(id, title);
                break;
            case ManagementActionKind.DictionaryItems:
                OpenDictionaryItems(id, title);
                break;
            case ManagementActionKind.AssignRoutes:
                await AssignRoutesAsync(id, title);
                break;
            case ManagementActionKind.TestProvider:
                var values = CustomPromptDialog.Show(Window.GetWindow(this), "测试通知渠道",
                    "输入测试目标（手机号或邮箱）；留空则使用当前账号。",
                    [new CustomPromptField("测试目标", Optional: true)], "发送测试");
                if (values is null) return;
                await RunActionAsync(async () =>
                {
                    var result = await _api.TestNotificationProviderAsync(id,
                        string.IsNullOrWhiteSpace(values[0]) ? null : values[0].Trim());
                    Status.Text = result.Succeeded
                        ? $"测试成功：{result.Status}"
                        : $"测试失败：{result.ErrorCode ?? result.Status ?? "未知错误"}";
                });
                break;
        }
    }

    private async Task AssignRolesAsync(Guid userId, string title)
    {
        try
        {
            var user = await _api.GetUserAsync(userId);
            var roles = await _api.GetRolesAsync();
            var options = roles.Items.Select(role => new SelectionOption(
                role.Id.ToString(),
                string.IsNullOrWhiteSpace(role.Name)
                    ? role.Code ?? role.Id.ToString()
                    : $"{role.Name}（{role.Code}）")).ToList();
            var selected = user.Roles.Select(role => role.Id.ToString()).ToHashSet();
            var dialog = new SelectionDialog($"分配角色 · {title}", options, selected)
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() != true) return;
            var roleIds = dialog.SelectedIds
                .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
                .Where(value => value != Guid.Empty)
                .ToArray();
            await RunActionAsync(async () =>
            {
                await _api.UpdateUserRolesAsync(user, roleIds);
                await LoadCoreAsync();
            }, "角色已保存");
        }
        catch (SessionExpiredException)
        {
            if (_sessionExpired is not null) _sessionExpired();
            else Status.Text = "登录已失效，请重新登录。";
        }
        catch (Exception error)
        {
            Status.Text = $"分配角色失败：{error.Message}";
        }
    }

    private async Task AssignRoutesAsync(Guid moduleId, string title)
    {
        try
        {
            var module = await _api.GetModuleAsync(moduleId);
            var routes = await _api.GetRouteOptionsAsync();
            var options = routes.Items.Select(route => new SelectionOption(
                route.Id.ToString(),
                string.IsNullOrWhiteSpace(route.Path)
                    ? route.Name ?? route.Id.ToString()
                    : $"{route.Name}（{route.Path}）",
                route.ParentId?.ToString())).ToList();
            var selected = module.RouteIds.ToHashSet();
            var dialog = new SelectionDialog($"分配路由 · {title}", options, selected, tree: true)
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() != true) return;
            var routeIds = dialog.SelectedIds
                .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
                .Where(value => value != Guid.Empty)
                .ToArray();
            await RunActionAsync(async () =>
            {
                await _api.UpdateModuleRoutesAsync(module, routeIds);
                await LoadCoreAsync();
            }, "模块路由已保存");
        }
        catch (SessionExpiredException)
        {
            if (_sessionExpired is not null) _sessionExpired();
            else Status.Text = "登录已失效，请重新登录。";
        }
        catch (Exception error)
        {
            Status.Text = $"分配路由失败：{error.Message}";
        }
    }

    private void OpenDictionaryItems(Guid dictionaryId, string title)
    {
        var definition = ManagementCatalog.DictionaryItems(dictionaryId, title);
        var window = new Window
        {
            Title = definition.Title,
            Width = 920,
            Height = 680,
            MinWidth = 720,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            Content = new ManagementPage(_api, definition, _sessionExpired)
        };
        window.SetResourceReference(Window.BackgroundProperty, "PageBrush");
        window.SetResourceReference(Window.ForegroundProperty, "InkBrush");
        window.ShowDialog();
    }

    private bool Confirm(string message) =>
        MessageBox.Show(Window.GetWindow(this), message, "确认操作",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private async Task RunActionAsync(Func<Task> action, string? success = null)
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        try
        {
            await action();
            if (success is not null) Status.Text = success;
        }
        catch (Exception error)
        {
            if (error is SessionExpiredException && _sessionExpired is not null) _sessionExpired();
            else Status.Text = $"操作失败：{error.Message}";
        }
        finally
        {
            _busy = false;
            UpdateButtons();
        }
    }

    private async Task ManageRelationAsync(ManagementRelation relation, ManagementRow row)
    {
        try
        {
            var optionsDocument = await _api.GetDocumentAsync(relation.OptionsPath);
            var optionItems = optionsDocument.ValueKind == JsonValueKind.Array
                ? optionsDocument : optionsDocument.GetProperty("items");
            var options = optionItems.EnumerateArray().Select(item => new SelectionOption(
                item.GetProperty("id").ToString(),
                item.TryGetProperty(relation.OptionLabelKey, out var label)
                    ? label.ToString() : item.GetProperty("id").ToString(),
                searchText: relation.OptionSearchKey is { } searchKey &&
                    item.TryGetProperty(searchKey, out var searchText)
                        ? searchText.ToString() : null)).ToList();
            var assignedDocument = await _api.GetDocumentAsync(relation.AssignedPath.Replace("{id}", row.Id));
            var assigned = assignedDocument.EnumerateArray().Select(item =>
                relation.AssignedAsObjects ? item.GetProperty("id").ToString() : item.ToString()).ToHashSet();
            var dialog = new SelectionDialog(relation.Label, options, assigned) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            await _api.SendDocumentAsync(HttpMethod.Post, relation.SavePath.Replace("{id}", row.Id), dialog.SelectedIds);
            Status.Text = $"{relation.Label}已保存";
        }
        catch (Exception error) { Status.Text = $"{relation.Label}失败：{error.Message}"; }
    }

    private async void RefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();
    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (Records is not null && Status is not null) ApplyFilter();
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        if (Records is null || Pager is null) return;
        var targets = TargetRows();
        var hasTarget = targets.Count > 0;
        CreateButton.IsEnabled = !_busy;
        DeleteButton.IsEnabled = !_busy && hasTarget && targets.All(row => !row.IsBuiltIn);
        foreach (var button in RowActions.Children.OfType<CustomButton>())
            button.IsEnabled = !_busy && hasTarget;
        Pager.IsEnabled = !_busy;
    }
}

public sealed class ManagementRow : System.ComponentModel.INotifyPropertyChanged
{
    public JsonElement Raw { get; }
    public string Id { get; }
    public bool IsBuiltIn { get; }
    public Dictionary<string, string> Values { get; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<ManagementRowAction> Actions { get; set; } = [];

    public ManagementRow(JsonElement raw, IReadOnlyList<ManagementField> fields)
    {
        Raw = raw;
        Id = raw.TryGetProperty("id", out var id) ? id.ToString() : string.Empty;
        IsBuiltIn = raw.TryGetProperty("isBuiltIn", out var builtIn) && builtIn.ValueKind == JsonValueKind.True;
        Values = fields.ToDictionary(field => field.Key, field =>
        {
            if (!raw.TryGetProperty(field.Key, out var value) || value.ValueKind == JsonValueKind.Null)
                return string.Empty;
            if (field.Options is not null && value.TryGetInt32(out var number))
                return field.Options.FirstOrDefault(option => option.Value == number)?.Label ?? value.ToString();
            return value.ToString();
        });
    }
}

public sealed class ManagementRowAction
{
    public required string Label { get; init; }

    /// <summary>edit / delete / action / relation。</summary>
    public required string Kind { get; init; }

    public required ManagementRow Row { get; init; }

    public ManagementAction? Action { get; init; }

    public ManagementRelation? Relation { get; init; }

    public bool Danger { get; init; }
}
