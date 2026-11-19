using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.Security;

public partial class SecurityPage : UserControl
{
    private const string DeletePhrase = "已了解注销事项";
    private readonly ApiClient _api;
    private readonly Action _signOut;
    private bool _busy;
    private bool _loaded;

    public SecurityPage(ApiClient api, Action signOut)
    {
        _api = api;
        _signOut = signOut;
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
        await RefreshTwoFactorAsync();
        await LoadSessionsAsync();
        Status.Text = "账号安全信息已更新";
    });

    private async Task RefreshTwoFactorAsync()
    {
        var status = await _api.GetTwoFactorStatusAsync();
        var count = new[] { status.AuthenticatorEnabled, status.EmailEnabled, status.SmsEnabled }.Count(x => x);
        TwoFactorSummary.Text = count == 0
            ? "尚未启用双重验证；建议至少启用一种方式。"
            : $"已启用 {count} 种方式 · 剩余恢复码 {status.RecoveryCodesRemaining} 个";
        ApplyFactor(AuthenticatorStatus, AuthenticatorButton, status.AuthenticatorEnabled, "身份验证器", null);
        ApplyFactor(EmailStatus, EmailButton, status.EmailEnabled, "邮件验证", status.MaskedEmail);
        ApplyFactor(SmsStatus, SmsButton, status.SmsEnabled, "短信验证", status.MaskedPhone);
    }

    private static void ApplyFactor(TextBlock text, CustomButton button, bool enabled, string label, string? mask)
    {
        text.Text = enabled
            ? $"{label}已启用{(string.IsNullOrWhiteSpace(mask) ? string.Empty : $"（{mask}）")}"
            : $"{label}未启用";
        button.Content = enabled ? "停用" : "启用";
        button.Tag = enabled;
    }

    private static string MethodLabel(string method) => method switch
    {
        "authenticator" => "身份验证器",
        "email" => "邮件验证",
        "sms" => "短信验证",
        _ => method
    };

    private async Task LoadSessionsAsync()
    {
        var sessions = await _api.GetMyLoginSessionsAsync();
        SessionGrid.ItemsSource = sessions
            .OrderByDescending(session => session.IsCurrent)
            .ThenByDescending(session => session.LastSeenAt)
            .ToList();
        SessionStatus.Text = $"共 {sessions.Count} 个会话";
    }

    private async void ChangePasswordClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var owner = Window.GetWindow(this);
        var values = CustomPromptDialog.Show(owner, "修改密码",
            "修改后当前会话会退出，请使用新密码重新登录。",
            [
                new CustomPromptField("当前密码", Secret: true),
                new CustomPromptField("新密码", Secret: true),
                new CustomPromptField("确认新密码", Secret: true)
            ], "修改密码");
        if (values is null) return;
        if (values[1].Length < 8) throw new InvalidOperationException("新密码至少需要 8 个字符。");
        if (values[1] != values[2]) throw new InvalidOperationException("两次输入的新密码不一致。");
        if (values[1] == values[0]) throw new InvalidOperationException("新密码不能与当前密码相同。");
        await _api.ChangePasswordAsync(values[0], values[1], values[2]);
        try { await _api.LogoutAsync(); } catch { }
        MessageBox.Show(owner, "密码已修改，请重新登录。", "修改密码", MessageBoxButton.OK, MessageBoxImage.Information);
        _signOut();
    });

    private async void AuthenticatorClick(object sender, RoutedEventArgs e) =>
        await ToggleAsync("authenticator", AuthenticatorButton);

    private async void EmailClick(object sender, RoutedEventArgs e) =>
        await ToggleAsync("email", EmailButton);

    private async void SmsClick(object sender, RoutedEventArgs e) =>
        await ToggleAsync("sms", SmsButton);

    private async Task ToggleAsync(string method, CustomButton button) => await RunAsync(async () =>
    {
        if (button.Tag is true) await DisableAsync(method);
        else await EnableAsync(method);
        await RefreshTwoFactorAsync();
    });

    private async Task EnableAsync(string method)
    {
        var owner = Window.GetWindow(this);
        var password = PromptPassword($"启用{MethodLabel(method)}", "请输入当前登录密码以继续。");
        if (password is null) return;

        if (method == "authenticator")
        {
            var setup = await _api.BeginAuthenticatorSetupAsync(password);
            if (string.IsNullOrWhiteSpace(setup.SharedKey) || string.IsNullOrWhiteSpace(setup.AuthenticatorUri))
                throw new InvalidOperationException("API 未返回身份验证器绑定信息。");
            var values = CustomPromptDialog.Show(owner, "绑定身份验证器",
                "将密钥添加到身份验证器应用，然后输入应用生成的 6 位验证码。",
                [
                    new CustomPromptField("密钥（可复制）", ReadOnly: true, Value: setup.SharedKey),
                    new CustomPromptField("绑定链接（可复制）", ReadOnly: true, Multiline: true, Value: setup.AuthenticatorUri),
                    new CustomPromptField("6 位验证码")
                ], "确认绑定");
            if (values is null) return;
            var codes = await _api.ConfirmAuthenticatorSetupAsync(values[2].Trim());
            if (codes.RecoveryCodes.Count > 0)
                CustomPromptDialog.Show(owner, "保存恢复码",
                    "这些一次性恢复码只显示一次，请立即离线保存；关闭后无法再次查看。",
                    [new CustomPromptField("恢复码", ReadOnly: true, Multiline: true,
                        Value: string.Join(Environment.NewLine, codes.RecoveryCodes))], "我已保存");
            Status.Text = "身份验证器已启用";
            return;
        }

        var challenge = await _api.RequestMessageTwoFactorSetupAsync(method, password);
        var challengeId = challenge.ChallengeId ?? throw new InvalidOperationException("API 未返回验证挑战。");
        var codeValues = CustomPromptDialog.Show(owner, $"启用{MethodLabel(method)}",
            $"验证码已发送至{MethodLabel(method)}绑定地址，请输入收到的验证码。",
            [new CustomPromptField("验证码")], "确认启用");
        if (codeValues is null) return;
        await _api.ConfirmMessageTwoFactorSetupAsync(challengeId, codeValues[0].Trim());
        Status.Text = $"{MethodLabel(method)}已启用";
    }

    private async Task DisableAsync(string method)
    {
        var owner = Window.GetWindow(this);
        var password = PromptPassword($"停用{MethodLabel(method)}", "请输入当前登录密码以确认停用。");
        if (password is null) return;
        if (MessageBox.Show(owner, $"确定停用{MethodLabel(method)}吗？", "停用双重验证",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        await _api.DisableTwoFactorAsync(method, password);
        Status.Text = $"{MethodLabel(method)}已停用";
    }

    private async void RefreshSessionsClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        await LoadSessionsAsync();
        Status.Text = "登录会话已更新";
    });

    private async void RevokeSessionClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (SessionGrid.SelectedItem is not LoginSession session)
            throw new InvalidOperationException("请先选择一个会话。");
        if (MessageBox.Show(Window.GetWindow(this), $"确定注销“{session.DeviceName ?? "所选设备"}”的会话吗？",
                "注销会话", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        await _api.RevokeMyLoginSessionAsync(session.Id);
        if (session.IsCurrent)
        {
            try { await _api.LogoutAsync(); } catch { }
            _signOut();
            return;
        }
        await LoadSessionsAsync();
        Status.Text = "所选会话已注销";
    });

    private async void DeleteAccountClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var owner = Window.GetWindow(this);
        var password = PromptPassword("注销账号", "此操作不可恢复。请输入当前密码继续。");
        if (password is null) return;
        var values = CustomPromptDialog.Show(owner, "确认注销",
            $"请输入「{DeletePhrase}」以确认永久删除当前账号。",
            [new CustomPromptField("确认短语")], "注销账号", danger: true);
        if (values is null) return;
        if (!string.Equals(values[0].Trim(), DeletePhrase, StringComparison.Ordinal))
            throw new InvalidOperationException("确认短语不匹配，已取消注销。");
        await _api.DeleteMyAccountAsync(password);
        try { await _api.LogoutAsync(); } catch { }
        MessageBox.Show(owner, "账号已注销。", "注销账号", MessageBoxButton.OK, MessageBoxImage.Information);
        _signOut();
    });

    private string? PromptPassword(string title, string description)
    {
        var values = CustomPromptDialog.Show(Window.GetWindow(this), title, description,
            [new CustomPromptField("当前密码", Secret: true)], "继续");
        return values?[0];
    }

    private void SessionSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void UpdateButtons()
    {
        if (ChangePasswordButton is null) return;
        ChangePasswordButton.IsEnabled = !_busy;
        AuthenticatorButton.IsEnabled = !_busy;
        EmailButton.IsEnabled = !_busy;
        SmsButton.IsEnabled = !_busy;
        RefreshSessionsButton.IsEnabled = !_busy;
        RevokeSessionButton.IsEnabled = !_busy && SessionGrid.SelectedItem is LoginSession;
        DeleteAccountButton.IsEnabled = !_busy;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        UpdateButtons();
        try { await action(); }
        catch (SessionExpiredException) { _signOut(); }
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
