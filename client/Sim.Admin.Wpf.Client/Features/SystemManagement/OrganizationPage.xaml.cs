using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Features.Common;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.SystemManagement;

public partial class OrganizationPage : UserControl
{
    private readonly ApiClient _api;
    private readonly Action? _sessionExpired;
    private List<OrganizationMember> _memberRows = [];
    private OrganizationUnit? _selected;
    private bool _busy;
    private bool _loaded;

    public OrganizationPage(ApiClient api, Action? sessionExpired = null)
    {
        _api = api;
        _sessionExpired = sessionExpired;
        InitializeComponent();
        Members.CheckedChanged += (_, _) => UpdateButtons();
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await LoadAsync();
        };
        UpdateButtons();
    }

    private async Task LoadAsync() => await RunAsync(async () =>
    {
        await LoadTreeAsync();
        await LoadMembersAsync();
        Status.Text = "组织架构已更新";
    });

    private async Task LoadTreeAsync()
    {
        var units = await _api.GetOrganizationTreeAsync();
        var nodes = units.ToDictionary(unit => unit.Id, unit => new OrganizationNode(unit));
        var roots = new List<OrganizationNode>();
        foreach (var node in nodes.Values)
        {
            if (node.Unit.ParentId is { } parentId &&
                nodes.TryGetValue(parentId, out var parent) && !ReferenceEquals(parent, node))
                parent.Children.Add(node);
            else roots.Add(node);
        }
        SortNodes(roots);
        OrgTree.ItemsSource = roots;
        Status.Text = $"共 {units.Count} 个组织";
        UpdateButtons();
    }

    private static void SortNodes(IEnumerable<OrganizationNode> nodes)
    {
        foreach (var node in nodes)
        {
            var ordered = node.Children.OrderBy(child => child.Unit.SortOrder)
                .ThenBy(child => child.Unit.Name).ToList();
            node.Children.Clear();
            foreach (var child in ordered) node.Children.Add(child);
            SortNodes(ordered);
        }
    }

    private async Task LoadMembersAsync()
    {
        if (_selected is null)
        {
            _memberRows = [];
            Members.ItemsSource = null;
            UpdateButtons();
            return;
        }
        var members = await _api.GetOrganizationMembersAsync(_selected.Id);
        _memberRows = members.ToList();
        Members.ItemsSource = _memberRows;
        UpdateButtons();
    }

    private List<OrganizationMember> CheckedMembers() =>
        Members.CheckedItems.Cast<OrganizationMember>().ToList();

    private async void OrgSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _selected = (e.NewValue as OrganizationNode)?.Unit;
        await RunAsync(LoadMembersAsync);
    }

    private async void CreateRootClick(object sender, RoutedEventArgs e) => await CreateNodeAsync(null);

    private async void CreateChildClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        await CreateNodeAsync(_selected.Id);
    }

    private async Task CreateNodeAsync(Guid? parentId)
    {
        var values = CustomPromptDialog.Show(Window.GetWindow(this), "新建部门",
            parentId is null ? "创建根部门。" : "在所选部门下创建子部门。",
            [
                new CustomPromptField("部门名称"),
                new CustomPromptField("部门代码"),
                new CustomPromptField("排序（可选）", Optional: true)
            ], "创建");
        if (values is null) return;
        var sort = 0;
        if (!string.IsNullOrWhiteSpace(values[2]) && !int.TryParse(values[2].Trim(), out sort))
        {
            Status.Text = "排序须为整数。";
            return;
        }
        await RunAsync(async () =>
        {
            await _api.CreateOrganizationAsync(new OrganizationCreate
            {
                ParentId = parentId,
                Name = values[0].Trim(),
                Code = values[1].Trim(),
                SortOrder = sort
            });
            await LoadTreeAsync();
            Status.Text = "部门已创建";
        });
    }

    private async void EditClick(object sender, RoutedEventArgs e)
    {
        if (_selected is not { } unit) return;
        var values = CustomPromptDialog.Show(Window.GetWindow(this), "编辑部门",
            $"修改“{unit.Name}”的名称和排序。",
            [
                new CustomPromptField("部门名称", Value: unit.Name),
                new CustomPromptField("排序", Value: unit.SortOrder.ToString(), Optional: true)
            ], "保存");
        if (values is null) return;
        var sort = unit.SortOrder;
        if (!string.IsNullOrWhiteSpace(values[1]) && !int.TryParse(values[1].Trim(), out sort))
        {
            Status.Text = "排序须为整数。";
            return;
        }
        await RunAsync(async () =>
        {
            await _api.UpdateOrganizationAsync(unit.Id, new OrganizationUpdate
            {
                Name = values[0].Trim(),
                ManagerUserId = unit.ManagerUserId,
                SortOrder = sort,
                IsActive = unit.IsActive
            });
            await LoadTreeAsync();
            Status.Text = "部门已保存";
        });
    }

    private async void DeleteClick(object sender, RoutedEventArgs e)
    {
        if (_selected is not { } unit) return;
        if (MessageBox.Show(Window.GetWindow(this),
                $"确定删除部门“{unit.Name}”吗？存在子部门或成员时服务端会拒绝。", "删除部门",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync(async () =>
        {
            await _api.DeleteOrganizationAsync(unit.Id);
            _selected = null;
            await LoadTreeAsync();
            await LoadMembersAsync();
            Status.Text = "部门已删除";
        });
    }

    private async void AddMemberClick(object sender, RoutedEventArgs e)
    {
        if (_selected is not { } unit) return;
        var user = UserPickerDialog.Pick(Window.GetWindow(this), $"选择成员 · {unit.Name}",
            async keyword => (await _api.GetSystemUsersAsync(keyword)).Items
                .Select(item => new UserOption(item.Id, item.UserName,
                    string.IsNullOrWhiteSpace(item.NickName) ? item.Name : item.NickName))
                .ToList());
        if (user is null) return;
        var values = CustomPromptDialog.Show(Window.GetWindow(this), "成员职位",
            "可填写职位名称，留空表示不设置。",
            [new CustomPromptField("职位", Optional: true)], "添加");
        if (values is null) return;
        await RunAsync(async () =>
        {
            await _api.AddOrganizationMemberAsync(new OrganizationMemberCreate
            {
                OrganizationUnitId = unit.Id,
                UserId = user.Id,
                IsPrimary = false,
                PositionName = string.IsNullOrWhiteSpace(values[0]) ? null : values[0].Trim()
            });
            await LoadMembersAsync();
            Status.Text = "成员已添加";
        });
    }

    private async void RemoveMemberClick(object sender, RoutedEventArgs e)
    {
        var members = CheckedMembers();
        if (members.Count == 0)
        {
            Status.Text = "请先勾选要移除的成员。";
            return;
        }
        var prompt = members.Count == 1
            ? "确定将该用户移出当前部门吗？"
            : $"确定将勾选的 {members.Count} 个成员移出当前部门吗？";
        if (MessageBox.Show(Window.GetWindow(this), prompt, "移除成员",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync(async () =>
        {
            foreach (var member in members) await _api.RemoveOrganizationMemberAsync(member.Id);
            await LoadMembersAsync();
            Status.Text = $"已移除 {members.Count} 个成员";
        });
    }

    private async void RefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void RefreshMembersClick(object sender, RoutedEventArgs e) =>
        await RunAsync(LoadMembersAsync);

    private void MemberSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        if (AddChildButton is null) return;
        var hasSelection = _selected is not null;
        AddChildButton.IsEnabled = !_busy && hasSelection;
        EditButton.IsEnabled = !_busy && hasSelection;
        DeleteButton.IsEnabled = !_busy && hasSelection;
        AddMemberButton.IsEnabled = !_busy && hasSelection;
        RemoveMemberButton.IsEnabled = !_busy && CheckedMembers().Count > 0;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        try { await action(); }
        catch (SessionExpiredException)
        {
            if (_sessionExpired is not null) _sessionExpired();
            else Status.Text = "登录已失效，请重新登录。";
        }
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

public sealed class OrganizationNode(OrganizationUnit unit)
{
    public OrganizationUnit Unit { get; } = unit;
    public ObservableCollection<OrganizationNode> Children { get; } = [];
    public string Display => string.IsNullOrWhiteSpace(Unit.Code)
        ? Unit.Name ?? Unit.Id.ToString()
        : $"{Unit.Name}（{Unit.Code}）";
}
