using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Controls.Inputs;
using Sim.Admin.Wpf.Client.Features.Common;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.Approval;

public enum ApprovalTaskActionKind
{
    Approve,
    Reject,
    ReturnToPrevious,
    ReturnToInitiator,
    Transfer,
    AddSign
}

public sealed record ApprovalActionRequest(
    ApprovalTaskActionKind Kind, string? Comment, Guid? TargetUserId, string Position);

public sealed class ApprovalActionDialog : Window
{
    private readonly ApiClient _api;
    private readonly ApprovalTask _task;
    private readonly List<(ApprovalTaskActionKind Kind, string Label)> _actions = [];
    private readonly CustomSelect _actionPicker = new() { Height = 38 };
    private readonly StackPanel _targetPanel = new() { Margin = new Thickness(0, 0, 0, 14) };
    private readonly TextBlock _targetText = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
    private readonly StackPanel _positionPanel = new() { Margin = new Thickness(0, 0, 0, 14) };
    private readonly CustomSelect _positionPicker = new() { Height = 38, SelectedIndex = 0 };
    private readonly CustomTextarea _comment = new() { Height = 100 };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private UserOption? _targetUser;

    public ApprovalActionRequest? Result { get; private set; }

    public static ApprovalActionRequest? Show(Window? owner, ApiClient api, ApprovalTask task)
    {
        var dialog = new ApprovalActionDialog(api, task) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private ApprovalActionDialog(ApiClient api, ApprovalTask task)
    {
        _api = api;
        _task = task;
        Title = "处理审批待办";
        Width = 540;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "PageBrush");
        SetResourceReference(ForegroundProperty, "InkBrush");

        _actions.Add((ApprovalTaskActionKind.Approve, "同意"));
        _actions.Add((ApprovalTaskActionKind.Reject, "驳回"));
        if (task.Permissions?.CanReturnToPrevious == true)
            _actions.Add((ApprovalTaskActionKind.ReturnToPrevious, "退回上一步"));
        if (task.Permissions?.CanReturnToInitiator == true)
            _actions.Add((ApprovalTaskActionKind.ReturnToInitiator, "退回发起人"));
        if (task.Permissions?.CanTransfer == true)
            _actions.Add((ApprovalTaskActionKind.Transfer, "转交"));
        if (task.Permissions?.CanAddSign == true)
            _actions.Add((ApprovalTaskActionKind.AddSign, "加签"));

        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(new TextBlock
        {
            Text = task.InstanceTitle ?? task.InstanceNumber ?? "所选待办",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        var meta = new TextBlock
        {
            Text = $"{task.InstanceNumber}  ·  {task.NodeName}",
            Margin = new Thickness(0, 6, 0, 18)
        };
        meta.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        body.Children.Add(meta);

        body.Children.Add(Label("处理动作"));
        _actionPicker.ItemsSource = _actions.Select(x => x.Label).ToList();
        _actionPicker.SelectedIndex = 0;
        _actionPicker.SelectionChanged += (_, _) => ApplyActionState();
        body.Children.Add(_actionPicker);

        var pickButton = new CustomButton { Content = "选择用户", Height = 34 };
        pickButton.Click += (_, _) =>
        {
            var user = UserPickerDialog.Pick(Window.GetWindow(this), "选择用户",
                async keyword => (await _api.GetApprovalUsersAsync(keyword))
                    .Select(item => new UserOption(item.Id, item.UserName, item.DisplayName))
                    .ToList());
            if (user is null) return;
            _targetUser = user;
            _targetText.Text = user.Label;
        };
        var targetRow = new StackPanel { Orientation = Orientation.Horizontal };
        targetRow.Children.Add(pickButton);
        targetRow.Children.Add(_targetText);
        _targetPanel.Children.Add(Label("目标用户"));
        _targetPanel.Children.Add(targetRow);
        body.Children.Add(_targetPanel);

        _positionPicker.ItemsSource = new[] { "之前", "之后" };
        _positionPanel.Children.Add(Label("加签位置"));
        _positionPanel.Children.Add(_positionPicker);
        body.Children.Add(_positionPanel);

        body.Children.Add(Label("处理意见（驳回、退回时必填）"));
        body.Children.Add(_comment);
        _error.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
        body.Children.Add(_error);

        var confirm = new CustomButton { Content = "确认处理", Variant = CustomButtonVariant.Primary, MinWidth = 100 };
        confirm.Click += (_, _) => Confirm();
        var cancel = new CustomButton { Content = "取消", MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        actions.Children.Add(confirm);
        actions.Children.Add(cancel);
        body.Children.Add(actions);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 720,
            Content = body
        };
        ApplyActionState();
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 0, 0, 6)
    };

    private ApprovalTaskActionKind SelectedAction =>
        _actions.Count == 0 ? ApprovalTaskActionKind.Approve : _actions[Math.Max(0, _actionPicker.SelectedIndex)].Kind;

    private void ApplyActionState()
    {
        var action = SelectedAction;
        var needsTarget = action is ApprovalTaskActionKind.Transfer or ApprovalTaskActionKind.AddSign;
        _targetPanel.Visibility = needsTarget ? Visibility.Visible : Visibility.Collapsed;
        _positionPanel.Visibility = action == ApprovalTaskActionKind.AddSign
            ? Visibility.Visible
            : Visibility.Collapsed;
        _error.Text = string.Empty;
    }

    private void Confirm()
    {
        var action = SelectedAction;
        var comment = _comment.Text.Trim();
        if (action is (ApprovalTaskActionKind.Transfer or ApprovalTaskActionKind.AddSign) && _targetUser is null)
        {
            _error.Text = "请选择目标用户。";
            return;
        }
        if (action is (ApprovalTaskActionKind.Reject or ApprovalTaskActionKind.ReturnToPrevious
                or ApprovalTaskActionKind.ReturnToInitiator) && comment.Length == 0)
        {
            _error.Text = "请填写处理意见。";
            return;
        }
        var position = action == ApprovalTaskActionKind.AddSign && _positionPicker.SelectedIndex == 1
            ? "After"
            : "Before";
        Result = new ApprovalActionRequest(action, comment.Length == 0 ? null : comment, _targetUser?.Id, position);
        DialogResult = true;
    }
}
