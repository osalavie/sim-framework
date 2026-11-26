using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.SystemManagement;

public partial class ScheduleTaskPage : UserControl
{
    private readonly ApiClient _api;
    private readonly Action? _sessionExpired;
    private List<ScheduleTask> _rows = [];
    private bool _busy;
    private bool _loaded;

    public ScheduleTaskPage(ApiClient api, Action? sessionExpired = null)
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
        var tasks = await _api.GetScheduleTasksAsync();
        _rows = tasks.ToList();
        Records.ItemsSource = _rows;
        Status.Text = $"共 {tasks.Count} 项任务 · 已启用 {tasks.Count(task => task.Enabled)} 项";
        UpdateButtons();
    }

    /// <summary>工具栏只处理复选框勾选的记录。</summary>
    private List<ScheduleTask> CheckedTasks() =>
        Records.CheckedItems.Cast<ScheduleTask>().ToList();

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private async void CreateClick(object sender, RoutedEventArgs e)
    {
        var input = ScheduleTaskDialog.Create(Window.GetWindow(this), _api);
        if (input is null) return;
        await RunAsync(async () =>
        {
            await _api.CreateScheduleTaskAsync(input);
            await LoadCoreAsync();
            Status.Text = "定时任务已创建";
        });
    }

    private async Task EditTaskAsync(ScheduleTask task)
    {
        var result = ScheduleTaskDialog.Edit(Window.GetWindow(this), task);
        if (result is null) return;
        await RunAsync(async () =>
        {
            var input = new ScheduleTask
            {
                Id = task.Id,
                Group = task.Group,
                Key = task.Key,
                CronExpression = result.CronExpression,
                Enabled = result.Enabled
            };
            await _api.UpdateScheduleTaskAsync(input);
            await LoadCoreAsync();
            Status.Text = "定时任务已保存";
        });
    }

    private async void TriggerClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var tasks = CheckedTasks();
        if (tasks.Count == 0) throw new InvalidOperationException("请先勾选要执行的定时任务。");
        var prompt = tasks.Count == 1
            ? $"确定立即执行“{tasks[0].DisplayName}”吗？"
            : $"确定立即执行勾选的 {tasks.Count} 个任务吗？";
        if (MessageBox.Show(Window.GetWindow(this), prompt, "立即执行",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        foreach (var task in tasks) await _api.TriggerScheduleTaskAsync(task.Id);
        Status.Text = $"已触发 {tasks.Count} 个任务，稍后刷新查看运行状态";
    });

    private async void DeleteClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var tasks = CheckedTasks();
        if (tasks.Count == 0) throw new InvalidOperationException("请先勾选要删除的定时任务。");
        if (tasks.Any(task => !task.CanDelete)) throw new InvalidOperationException("内置任务不可删除。");
        var prompt = tasks.Count == 1
            ? $"确定删除“{tasks[0].DisplayName}”吗？"
            : $"确定删除勾选的 {tasks.Count} 个任务吗？";
        if (MessageBox.Show(Window.GetWindow(this), prompt, "删除定时任务",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        foreach (var task in tasks) await _api.DeleteScheduleTaskAsync(task.Id);
        await LoadCoreAsync();
        Status.Text = $"已删除 {tasks.Count} 个定时任务";
    });

    private async void RefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private async void RecordsDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Records.SelectedItem is ScheduleTask task) await EditTaskAsync(task);
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        if (TriggerButton is null) return;
        var tasks = CheckedTasks();
        TriggerButton.IsEnabled = !_busy && tasks.Count > 0;
        DeleteButton.IsEnabled = !_busy && tasks.Count > 0 && tasks.All(task => task.CanDelete);
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
