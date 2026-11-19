using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Converters;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.Approval;

public partial class ApprovalDetailWindow : Window
{
    private readonly ApiClient _api;
    private readonly Guid _instanceId;
    private readonly bool _fromMonitor;
    private readonly Action _sessionExpired;
    private ApprovalInstanceDetail? _detail;
    private ApprovalTask? _myTask;
    private bool _busy;
    private bool _loaded;

    public bool Changed { get; private set; }

    public ApprovalDetailWindow(ApiClient api, Guid instanceId, bool fromMonitor, Action sessionExpired)
    {
        _api = api;
        _instanceId = instanceId;
        _fromMonitor = fromMonitor;
        _sessionExpired = sessionExpired;
        InitializeComponent();
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
        await LoadCoreAsync();
        Status.Text = "审批详情已更新";
    });

    private async Task LoadCoreAsync()
    {
        _detail = _fromMonitor
            ? await _api.GetApprovalMonitorDetailAsync(_instanceId)
            : await _api.GetApprovalDetailAsync(_instanceId);
        ApplyDetail();
    }

    private void ApplyDetail()
    {
        if (_detail is not { } detail) return;
        TitleText.Text = detail.Title ?? detail.Number ?? "审批详情";
        NumberText.Text = detail.Number ?? string.Empty;
        DefinitionText.Text = string.IsNullOrWhiteSpace(detail.DefinitionName)
            ? detail.DefinitionCode ?? string.Empty
            : $"{detail.DefinitionName} · v{detail.DefinitionVersion}";
        StatusText.Text = ApprovalStatusConverter.Label(detail.Status);
        DefinitionValue.Text = string.IsNullOrWhiteSpace(detail.DefinitionCode)
            ? detail.DefinitionName ?? string.Empty
            : $"{detail.DefinitionName}（{detail.DefinitionCode}）v{detail.DefinitionVersion}";
        BusinessValue.Text = detail.BusinessType ?? string.Empty;
        InitiatorValue.Text = detail.InitiatorUserId?.ToString() ?? string.Empty;
        NodeValue.Text = detail.CurrentNodeName ?? detail.CurrentNodeKey ?? string.Empty;
        CreatedValue.Text = detail.CreationTime?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
        CompletedValue.Text = detail.CompletedTime?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
        BusinessIdValue.Text = detail.BusinessId ?? string.Empty;

        ContextGrid.ItemsSource = detail.Context?.Fields ?? new List<ApprovalContextField>();
        NodeGrid.ItemsSource = detail.Nodes;
        RecordGrid.ItemsSource = detail.Records;
        AttachmentGrid.ItemsSource = detail.Attachments;
        CcGrid.ItemsSource = detail.CcRecipients;

        _myTask = detail.Tasks.FirstOrDefault(task =>
            string.Equals(task.Status, "Pending", StringComparison.Ordinal) &&
            task.AssigneeUserId == _api.CurrentUserId);
        var running = string.Equals(detail.Status, "Running", StringComparison.Ordinal);
        HandleButton.Visibility = _myTask is null ? Visibility.Collapsed : Visibility.Visible;
        WithdrawButton.Visibility = detail.CanWithdraw ? Visibility.Visible : Visibility.Collapsed;
        ResubmitButton.Visibility = detail.CanResubmit ? Visibility.Visible : Visibility.Collapsed;
        UrgeButton.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        UploadButton.Visibility = detail.CanUploadAttachment ? Visibility.Visible : Visibility.Collapsed;
        TaskUploadButton.Visibility = _myTask is null ? Visibility.Collapsed : Visibility.Visible;
        TerminateButton.Visibility = _fromMonitor && running ? Visibility.Visible : Visibility.Collapsed;
        UpdateButtons();
    }

    private async void HandleClick(object sender, RoutedEventArgs e)
    {
        if (_myTask is not { } task) return;
        var request = ApprovalActionDialog.Show(this, _api, task);
        if (request is null) return;
        await RunAsync(async () =>
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
            Changed = true;
            await LoadCoreAsync();
            Status.Text = "待办已处理";
        });
    }

    private async void WithdrawClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var values = CustomPromptDialog.Show(this, "撤回审批",
            "撤回后可重新编辑并提交；可填写撤回说明。",
            [new CustomPromptField("撤回说明", Multiline: true, Optional: true)], "撤回");
        if (values is null) return;
        await _api.WithdrawApprovalAsync(_instanceId,
            string.IsNullOrWhiteSpace(values[0]) ? null : values[0].Trim());
        Changed = true;
        await LoadCoreAsync();
        Status.Text = "审批已撤回";
    });

    private async void ResubmitClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (_detail is not { } detail) return;
        var values = CustomPromptDialog.Show(this, "重新提交",
            "可修改标题后重新提交；原业务字段保持不变。",
            [new CustomPromptField("标题", Value: detail.Title)], "重新提交");
        if (values is null) return;
        var title = values[0].Trim();
        if (title.Length == 0) throw new InvalidOperationException("请填写标题。");
        var context = detail.Context?.Fields
            .Where(field => !string.IsNullOrWhiteSpace(field.Key))
            .ToDictionary(field => field.Key!, field => field.Value ?? string.Empty)
            ?? new Dictionary<string, string>();
        await _api.ResubmitApprovalAsync(_instanceId, detail.BusinessType ?? string.Empty,
            detail.BusinessId ?? string.Empty, title, context);
        Changed = true;
        await LoadCoreAsync();
        Status.Text = "审批已重新提交";
    });

    private async void UrgeClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "确定向当前审批人发送催办提醒吗？", "催办",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        await RunAsync(async () =>
        {
            await _api.UrgeApprovalAsync(_instanceId);
            Status.Text = "已发送催办提醒";
        });
    }

    private async void TerminateClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var values = CustomPromptDialog.Show(this, "终止流程",
            "终止后流程立即结束且不可恢复，请填写终止原因。",
            [new CustomPromptField("终止原因", Multiline: true)], "终止流程", danger: true);
        if (values is null) return;
        await _api.TerminateApprovalAsync(_instanceId, values[0].Trim());
        Changed = true;
        await LoadCoreAsync();
        Status.Text = "流程已终止";
    });

    private async void UploadClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择附件", Filter = "所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        await RunAsync(async () =>
        {
            await _api.UploadApprovalAttachmentAsync(_instanceId, dialog.FileName);
            Changed = true;
            await LoadCoreAsync();
            Status.Text = "附件已上传";
        });
    }

    private async void TaskUploadClick(object sender, RoutedEventArgs e)
    {
        if (_myTask is not { } task) return;
        var dialog = new OpenFileDialog { Title = "选择待办附件", Filter = "所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        await RunAsync(async () =>
        {
            await _api.UploadApprovalTaskAttachmentAsync(task.Id, dialog.FileName);
            Changed = true;
            await LoadCoreAsync();
            Status.Text = "待办附件已上传";
        });
    }

    private async void DownloadClick(object sender, RoutedEventArgs e)
    {
        if (AttachmentGrid.SelectedItem is not ApprovalAttachment attachment) return;
        var dialog = new SaveFileDialog
        {
            Title = "保存附件",
            FileName = attachment.FileName ?? $"attachment-{attachment.Id:D}"
        };
        if (dialog.ShowDialog(this) != true) return;
        await RunAsync(async () =>
        {
            var bytes = await _api.DownloadApprovalAttachmentAsync(attachment.Id);
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            Status.Text = $"附件已保存到 {dialog.FileName}";
        });
    }

    private async void RefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private void AttachmentSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateButtons();

    private void UpdateButtons()
    {
        if (HandleButton is null) return;
        HandleButton.IsEnabled = !_busy && _myTask is not null;
        WithdrawButton.IsEnabled = !_busy;
        ResubmitButton.IsEnabled = !_busy;
        UrgeButton.IsEnabled = !_busy;
        UploadButton.IsEnabled = !_busy;
        TaskUploadButton.IsEnabled = !_busy && _myTask is not null;
        TerminateButton.IsEnabled = !_busy;
        DownloadButton.IsEnabled = !_busy && AttachmentGrid.SelectedItem is ApprovalAttachment;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        try { await action(); }
        catch (SessionExpiredException)
        {
            _sessionExpired();
            Close();
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
