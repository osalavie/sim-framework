using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Features.Management;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.SystemManagement;

public partial class TenantPage : UserControl
{
    private readonly ApiClient _api;
    private readonly Action? _sessionExpired;
    private List<TenantInfo> _rows = [];
    private bool _busy;
    private bool _loaded;

    public TenantPage(ApiClient api, Action? sessionExpired = null)
    {
        _api = api;
        _sessionExpired = sessionExpired;
        InitializeComponent();
        Records.CheckedChanged += (_, _) => UpdateButtons();
        Loaded += async (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            await LoadAsync();
        };
        UpdateButtons();
    }

    private async Task LoadCoreAsync()
    {
        var tenants = await _api.GetTenantsAsync();
        _rows = tenants.ToList();
        Records.ItemsSource = _rows;
        Status.Text = $"共 {tenants.Count} 个租户 · 启用 {tenants.Count(t => t.IsActive)} 个";
        UpdateButtons();
    }

    /// <summary>工具栏只处理复选框勾选的记录。</summary>
    private List<TenantInfo> CheckedTenants() =>
        Records.CheckedItems.Cast<TenantInfo>().ToList();

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async void CreateClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var owner = Window.GetWindow(this);
        var values = CustomPromptDialog.Show(owner, "新建租户",
            "填写租户基本信息；管理员用户名和初始密码留空时使用系统默认值。",
            [
                new CustomPromptField("租户名称"),
                new CustomPromptField("租户代码"),
                new CustomPromptField("管理员邮箱", Optional: true),
                new CustomPromptField("管理员用户名", Optional: true, Value: "admin"),
                new CustomPromptField("管理员初始密码", Secret: true, Optional: true)
            ], "下一步");
        if (values is null) return;
        var templates = await _api.GetTenantTemplatesAsync();
        var moduleIds = SelectTemplate("选择租户模块",
            templates.Modules.Select(item => new SelectionOption(item.Id.ToString(), item.Name ?? item.Id.ToString())),
            templates.Modules.Select(item => item.Id));
        if (moduleIds is null) return;
        var routeIds = SelectTemplate("选择租户路由",
            templates.Routes.Select(item => new SelectionOption(item.Id.ToString(),
                $"{item.Name}（{item.Path}）")),
            templates.Routes.Select(item => item.Id));
        if (routeIds is null) return;
        var permissionIds = SelectTemplate("选择租户权限",
            templates.Permissions.Select(item => new SelectionOption(item.Id.ToString(),
                $"{item.Name}（{item.Code}）")),
            templates.Permissions.Select(item => item.Id));
        if (permissionIds is null) return;

        var result = await _api.CreateTenantAsync(new TenantCreate
        {
            Name = values[0].Trim(),
            Code = values[1].Trim(),
            AdminEmail = EmptyToNull(values[2]),
            AdminUserName = EmptyToNull(values[3]) ?? "admin",
            AdminPassword = EmptyToNull(values[4]),
            ModuleIds = moduleIds,
            RouteIds = routeIds,
            PermissionIds = permissionIds
        });
        await LoadCoreAsync();
        MessageBox.Show(owner,
            $"租户已创建。\n管理员：{result.AdminUserName}\n初始密码：{result.InitialPassword}",
            "租户创建成功", MessageBoxButton.OK, MessageBoxImage.Information);
        Status.Text = $"租户“{result.TenantName}”已创建";
    });

    private List<Guid>? SelectTemplate(string title, IEnumerable<SelectionOption> options,
        IEnumerable<Guid> selectedIds)
    {
        var list = options.ToList();
        var selected = selectedIds.Select(id => id.ToString()).ToHashSet();
        var dialog = new SelectionDialog(title, list, selected) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return null;
        return dialog.SelectedIds
            .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .ToList();
    }

    private async Task EditTenantAsync(TenantInfo tenant)
    {
        var values = CustomPromptDialog.Show(Window.GetWindow(this), "编辑租户",
            "仅更新非空字段。",
            [
                new CustomPromptField("租户名称", Value: tenant.Name),
                new CustomPromptField("管理员邮箱", Value: tenant.AdminEmail, Optional: true),
                new CustomPromptField("连接字符串", Value: tenant.ConnectionString,
                    Optional: true, Multiline: true)
            ], "保存");
        if (values is null) return;
        await RunAsync(async () =>
        {
            await _api.UpdateTenantAsync(tenant.Id, new TenantUpdate
            {
                Name = EmptyToNull(values[0]),
                AdminEmail = EmptyToNull(values[1]),
                ConnectionString = EmptyToNull(values[2])
            });
            await LoadCoreAsync();
            Status.Text = "租户已保存";
        });
    }

    private async void AssignmentsClick(object sender, RoutedEventArgs e)
    {
        var tenant = CheckedTenants().FirstOrDefault();
        if (tenant is null)
        {
            Status.Text = "请先勾选一个租户。";
            return;
        }
        await RunAsync(async () =>
        {
            var assignments = await _api.GetTenantAssignmentsAsync(tenant.Id);
            new TenantAssignmentsWindow(assignments) { Owner = Window.GetWindow(this) }.ShowDialog();
        });
    }

    private async void EnableClick(object sender, RoutedEventArgs e) => await ChangeActiveAsync(true);
    private async void DisableClick(object sender, RoutedEventArgs e) => await ChangeActiveAsync(false);

    private async Task ChangeActiveAsync(bool active)
    {
        var tenants = CheckedTenants();
        if (tenants.Count == 0)
        {
            Status.Text = "请先勾选要操作的租户。";
            return;
        }
        var action = active ? "启用" : "停用";
        var prompt = tenants.Count == 1
            ? $"确定{action}租户“{tenants[0].Name}”吗？"
            : $"确定{action}勾选的 {tenants.Count} 个租户吗？";
        if (MessageBox.Show(Window.GetWindow(this), prompt, $"{action}租户",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync(async () =>
        {
            foreach (var tenant in tenants)
            {
                if (active) await _api.EnableTenantAsync(tenant.Id);
                else await _api.DisableTenantAsync(tenant.Id);
            }
            await LoadCoreAsync();
            Status.Text = $"已{action} {tenants.Count} 个租户";
        });
    }

    private async void DeleteClick(object sender, RoutedEventArgs e)
    {
        var tenants = CheckedTenants();
        if (tenants.Count == 0)
        {
            Status.Text = "请先勾选要删除的租户。";
            return;
        }
        var prompt = tenants.Count == 1
            ? $"确定删除租户“{tenants[0].Name}”吗？该操作不可恢复。"
            : $"确定删除勾选的 {tenants.Count} 个租户吗？该操作不可恢复。";
        if (MessageBox.Show(Window.GetWindow(this), prompt, "删除租户",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunAsync(async () =>
        {
            foreach (var tenant in tenants) await _api.DeleteTenantAsync(tenant.Id);
            await LoadCoreAsync();
            Status.Text = $"已删除 {tenants.Count} 个租户";
        });
    }

    private async void RefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void RecordsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Records.SelectedItem is TenantInfo tenant) await EditTenantAsync(tenant);
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        if (AssignmentsButton is null) return;
        var checkedTenants = CheckedTenants();
        var hasChecked = checkedTenants.Count > 0;
        AssignmentsButton.IsEnabled = !_busy && hasChecked;
        EnableButton.IsEnabled = !_busy && checkedTenants.Any(tenant => !tenant.IsActive);
        DisableButton.IsEnabled = !_busy && checkedTenants.Any(tenant => tenant.IsActive);
        DeleteButton.IsEnabled = !_busy && hasChecked;
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
