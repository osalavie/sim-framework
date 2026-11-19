using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Sim.Admin.Wpf.Client.Business;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Controls.Container;
using Sim.Admin.Wpf.Client.Controls.Inputs;
using Sim.Admin.Wpf.Client.Controls.Overlay;
using Sim.Admin.Wpf.Client.Services;
using Sim.Admin.Wpf.Client.Theme;
using Sim.Admin.Wpf.Client.Features.Approval;
using Sim.Admin.Wpf.Client.Features.Authorization;
using Sim.Admin.Wpf.Client.Features.Components;
using Sim.Admin.Wpf.Client.Features.Security;
using Sim.Admin.Wpf.Client.Features.SystemManagement;

namespace Sim.Admin.Wpf.Client;

public partial class MainWindow : Window
{
    private static readonly CustomMenuNode[] PlatformMenuEntries =
    [
        new("home", "工作台", "\uE80F"),
        new("approval", "审批", "\uE9D5",
            new("approval.todo", "我的待办", "\uE8FD"),
            new("approval.handled", "我的已办", "\uE930"),
            new("approval.submitted", "我发起的", "\uE724"),
            new("approval.cc", "抄送我的", "\uE715"),
            new("approval.definitions", "流程定义", "\uE8A5"),
            new("approval.monitor", "实例监控", "\uE9D9")),
        new("system", "系统管理", "\uE713",
            new("system.storage", "文件存储", "\uE8B7"),
            new("system.schedule", "定时任务", "\uE823"),
            new("system.notifications", "通知服务", "\uE7ED"),
            new("system.numbering", "业务编号", "\uE8C8"),
            new("system.config", "系统配置", "\uE9F9"),
            new("system.dictionary", "数据字典", "\uE8EF"),
            new("system.organization", "组织架构", "\uE902"),
            new("system.tenant", "租户管理", "\uE821")),
        new("authorization", "授权管理", "\uE192",
            new("auth.user", "用户管理", "\uE77B"),
            new("auth.role", "角色管理", "\uE716"),
            new("auth.module", "模块管理", "\uE71D"),
            new("auth.route", "路由管理", "\uE968"),
            new("auth.permission", "权限管理", "\uE8D7"))
    ];

    private static readonly CustomMenuNode[] MenuEntries = BuildMenuEntries();

    private static readonly CustomMenuNode[] PersonalEntries =
    [
        new("profile", "个人资料", "\uE77B"),
        new("security", "账号安全", "\uE72E"),
        new("notifications", "通知中心", "\uE7ED")
    ];

    private static readonly string[] ThemeDescriptions =
    [
        "奶油纸底与珊瑚行动色，默认风格。",
        "深青底与金线，沉稳克制。",
        "青绿与月白，清透干净。",
        "朱红与宣纸，庄重典雅。"
    ];

    private static CustomMenuNode[] BuildMenuEntries()
    {
        var business = BusinessModuleRegistration.Modules
            .OrderBy(module => module.Order)
            .Select(module => new CustomMenuNode(
                module.Key, module.Title, module.Glyph,
                module.Menus.Select(item => new CustomMenuNode(item.Key, item.Title, item.Glyph)).ToArray()))
            .ToArray();
        return business.Length == 0 ? PlatformMenuEntries : [.. PlatformMenuEntries, .. business];
    }

    private readonly ApiClient _api = new();
    private readonly Dictionary<string, IBusinessWorkspace> _businessWorkspaces = [];
    private readonly Dictionary<string, TabItem> _businessTabs = [];
    private string _sessionUserName = string.Empty;
    private readonly string? _apiBaseUrl;
    private readonly string? _apiConfigError;
    private AuthorizationWorkspace? _authorization;
    private SystemWorkspace? _system;
    private ApprovalWorkspace? _approval;
    private Guid? _challengeId;
    private string _loginUserName = string.Empty;
    private string _loginPassword = string.Empty;
    private bool _rememberMe;
    private bool _busy;
    private bool _themeFromServer;
    private bool _signedIn;
    private int _themeStyle;
    private bool _darkMode;
    private readonly SemaphoreSlim _themeSaveGate = new(1, 1);

    public MainWindow()
    {
        InitializeComponent();
        ProfileAvatar.Configure(_api, png => _api.UploadMyAvatarAsync(png));
        ProfileAvatar.AvatarChanged += async (_, url) => await ReloadNavAvatarAsync(url);
        (_apiBaseUrl, _apiConfigError) = LoadApiBaseUrl();
        SetThemeState(0, false);
        CopyrightYear.Text = DateTime.Now.Year.ToString();
        MainNav.ItemsSource = MenuEntries;
        Show(LoginPanel);
        PrefillRememberedLogin();
        Closed += (_, _) => _api.Dispose();

        // --gallery 直接打开组件库预览，便于设计评审与视觉回归；--dark 以玄夜配色启动。
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Any(x => string.Equals(x, "--dark", StringComparison.OrdinalIgnoreCase)))
            SetThemeState(_themeStyle, true);

        if (arguments.Any(x => string.Equals(x, "--gallery", StringComparison.OrdinalIgnoreCase)))
        {
            Loaded += (_, _) => OpenGalleryClick(this, new RoutedEventArgs());
        }
    }

    private static (string? Url, string? Error) LoadApiBaseUrl()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var url = document.RootElement.GetProperty("Api").GetProperty("BaseUrl").GetString();
            if (!string.IsNullOrWhiteSpace(url)) return (url.Trim(), null);
            return (null, $"API 地址未配置：{path} 的 Api:BaseUrl 为空。");
        }
        catch (Exception exception) when (exception is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return (null, $"未读取到 API 配置，请检查 {path}。");
        }
    }

    private async void DarkModeButtonClick(object sender, RoutedEventArgs e)
    {
        SetThemeState(_themeStyle, !_darkMode);
        await SaveThemeAsync();
    }

    private void SetThemeState(int style, bool dark)
    {
        _themeStyle = Math.Clamp(style, 0, ThemeManager.Styles.Length - 1);
        _darkMode = dark;
        var glyph = _darkMode ? "\uE706" : "\uE708";
        var toolTip = _darkMode ? "切换到月白" : "切换到玄夜";
        DarkModeGlyph.Text = glyph;
        DarkModeButton.ToolTip = toolTip;
        LoginDarkModeGlyph.Text = glyph;
        LoginDarkModeButton.ToolTip = toolTip;
        ThemePreferenceValue.Text = ThemeManager.Styles[_themeStyle];
        ThemeManager.Apply(_themeStyle, _darkMode);
    }

    private async Task SaveThemeAsync()
    {
        if (!_signedIn || _themeFromServer) return;
        await _themeSaveGate.WaitAsync();
        try
        {
            if (!_signedIn) return;
            await _api.UpdateMyPreferencesAsync(ThemeManager.Ids[_themeStyle], _darkMode);
        }
        catch (SessionExpiredException) { EndSession("登录已失效，请重新登录。"); }
        catch (Exception error) { CustomMessage.TopError(this, error.Message); }
        finally { _themeSaveGate.Release(); }
    }

    private void OpenGalleryClick(object sender, RoutedEventArgs e)
    {
        var gallery = new ComponentsGalleryWindow { Owner = this };
        gallery.Show();
    }

    private void MainNavChanged(object sender, CustomMenuSelectedEventArgs e) => ActivateLeaf(e.Node);

    private void ActivateLeaf(CustomMenuNode node)
    {
        var key = node.Key;
        switch (key)
        {
            case "home": ShowPage(0, node); break;
            case var k when k.StartsWith("auth.", StringComparison.Ordinal):
                ShowPage(4, node);
                _authorization?.ShowModule(k["auth.".Length..]);
                break;
            case var k when k.StartsWith("system.", StringComparison.Ordinal):
                ShowPage(5, node);
                _system?.ShowModule(k["system.".Length..]);
                break;
            case var k when k.StartsWith("approval.", StringComparison.Ordinal):
                ShowPage(6, node);
                _approval?.ShowModule(k["approval.".Length..]);
                break;
            default:
            {
                var module = BusinessModuleRegistration.Modules.FirstOrDefault(
                    item => key.StartsWith(item.Key + ".", StringComparison.Ordinal));
                if (module is not null) ActivateBusinessLeaf(module, key);
                break;
            }
        }
    }

    private async void ActivateBusinessLeaf(IBusinessClientModule module, string menuKey)
    {
        try
        {
            var workspace = EnsureBusinessWorkspace(module);
            WorkspaceTabs.SelectedItem = _businessTabs[module.Key];
            BreadcrumbGroup.Text = module.Title;
            BreadcrumbGroup.Visibility = Visibility.Visible;
            BreadcrumbSeparator.Visibility = Visibility.Visible;
            BreadcrumbCurrent.Text = module.Menus.FirstOrDefault(item => item.Key == menuKey)?.Title ?? module.Title;
            await workspace.ShowAsync(menuKey);
        }
        catch (SessionExpiredException) { EndSession("登录已失效，请重新登录。"); }
        catch (Exception error) { CustomMessage.TopError(this, error.Message); }
    }

    private IBusinessWorkspace EnsureBusinessWorkspace(IBusinessClientModule module)
    {
        if (_businessWorkspaces.TryGetValue(module.Key, out var existing)) return existing;
        var workspace = module.CreateWorkspace(new BusinessClientContext
        {
            Api = _api,
            UserId = _api.CurrentUserId ?? throw new InvalidOperationException("请先登录。"),
            UserName = _sessionUserName,
            EndSession = EndSession
        });
        var tab = new TabItem { Header = module.Title, Content = workspace.View };
        _businessWorkspaces[module.Key] = workspace;
        _businessTabs[module.Key] = tab;
        WorkspaceTabs.Items.Add(tab);
        return workspace;
    }

    private void ShowPage(int index, CustomMenuNode node)
    {
        WorkspaceTabs.SelectedIndex = index;
        var group = MenuEntries.FirstOrDefault(x => x.Children.Any(child => child.Key == node.Key));
        BreadcrumbGroup.Text = group?.Label ?? string.Empty;
        BreadcrumbGroup.Visibility = group is null ? Visibility.Collapsed : Visibility.Visible;
        BreadcrumbSeparator.Visibility = group is null ? Visibility.Collapsed : Visibility.Visible;
        BreadcrumbCurrent.Text = node.Label;
    }

    private void TogglePersonalMenuClick(object sender, RoutedEventArgs e) =>
        PersonalMenuPopup.IsOpen = !PersonalMenuPopup.IsOpen;

    private void PersonalMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string key }) return;
        PersonalMenuPopup.IsOpen = false;
        var node = PersonalEntries.First(x => x.Key == key);
        var index = key switch { "profile" => 1, "security" => 2, _ => 3 };
        ShowPage(index, node);
        MainNav.ClearSelection();
    }

    private void ThemeMenuItemClick(object sender, RoutedEventArgs e)
    {
        PersonalMenuPopup.IsOpen = false;
        ShowThemePickerDialog();
    }

    private void ShowThemePickerDialog() => CreateThemePickerWindow().ShowDialog();

    private Window CreateThemePickerWindow()
    {
        var window = CustomOverlayBuilder.CreateWindow<Window>(this, "选择主题", 400);
        window.SizeToContent = SizeToContent.Height;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(CustomOverlayBuilder.CreateTitle("选择主题"));
        var hint = new TextBlock
        {
            Text = "主题会同步到当前账号。",
            FontSize = 12,
            Margin = new Thickness(0, 6, 0, 16)
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        body.Children.Add(hint);

        for (var i = 0; i < ThemeManager.Styles.Length; i++)
        {
            var index = i;
            var label = new StackPanel();
            label.Children.Add(new TextBlock { Text = ThemeManager.Styles[i], FontWeight = FontWeights.Bold });
            var description = new TextBlock
            {
                Text = ThemeDescriptions[i],
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0)
            };
            description.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            label.Children.Add(description);
            var radio = new CustomRadio
            {
                Content = label,
                GroupName = "ThemeStyle",
                IsChecked = i == _themeStyle,
                Margin = new Thickness(0, 0, 0, 12)
            };
            radio.Checked += (_, _) => SetThemeState(index, _darkMode);
            body.Children.Add(radio);
        }

        var done = CustomOverlayBuilder.CreateButton("完成", CustomButtonVariant.Primary);
        done.IsDefault = true;
        done.IsCancel = true;
        done.Click += (_, _) => window.Close();
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        actions.Children.Add(done);
        body.Children.Add(actions);

        window.Closed += (_, _) => _ = SaveThemeAsync();
        window.Content = body;
        return window;
    }

    private async void LoginClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var baseUrl = _apiBaseUrl;
        if (baseUrl is null) throw new InvalidOperationException(_apiConfigError ?? "API 地址未配置。");
        _loginUserName = UserName.Text.Trim();
        _loginPassword = Password.Password;
        if (string.IsNullOrWhiteSpace(_loginUserName) || string.IsNullOrWhiteSpace(_loginPassword))
            throw new InvalidOperationException("请输入用户名和密码。");
        _rememberMe = RememberMe.IsChecked == true;
        _api.Connect(baseUrl);
        var result = await _api.LoginAsync(_loginUserName, _loginPassword, _rememberMe);
        Password.Clear();
        if (result.RequiresTwoFactor)
        {
            _challengeId = result.TwoFactorChallengeId ?? throw new InvalidOperationException("API 未返回验证挑战。");
            FactorMethod.ItemsSource = result.TwoFactorMethods;
            FactorMethod.SelectedIndex = 0;
            Show(FactorPanel);
        }
        else await AcceptSessionAsync(result);
    });

    private async void SendCodeClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var method = FactorMethod.SelectedItem as string;
        if (_challengeId is null || method is null) throw new InvalidOperationException("请选择验证方式。");
        if (method is not ("email" or "sms")) throw new InvalidOperationException("此方式无需发送验证码，请直接输入。 ");
        await _api.SendTwoFactorCodeAsync(_challengeId.Value, method);
        CustomMessage.TopInfo(this, "验证码已发送");
    });

    private async void CompleteFactorClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var method = FactorMethod.SelectedItem as string;
        if (_challengeId is null || method is null || string.IsNullOrWhiteSpace(FactorCode.Text))
            throw new InvalidOperationException("请选择方式并输入验证码。");
        var result = await _api.CompleteTwoFactorAsync(_challengeId.Value, method, FactorCode.Text.Trim(), _rememberMe);
        FactorCode.Clear();
        _challengeId = null;
        await AcceptSessionAsync(result);
    });

    private void BackToLoginClick(object sender, RoutedEventArgs e)
    {
        _challengeId = null;
        FactorCode.Clear();
        Show(LoginPanel);
    }

    private async void LogoutClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        try { await _api.LogoutAsync(); } catch { }
        finally { EndSession(null); }
    });

    private async Task AcceptSessionAsync(LoginResult result)
    {
        _api.AcceptSession(result);
        _sessionUserName = result.UserName ?? string.Empty;
        _signedIn = true;
        var sessionExpired = new Action(() => EndSession("登录已失效，请重新登录。"));
        _authorization = new AuthorizationWorkspace(_api, sessionExpired);
        _system = new SystemWorkspace(_api, sessionExpired);
        _approval = new ApprovalWorkspace(_api, sessionExpired);
        AuthorizationHost.Content = _authorization;
        SystemHost.Content = _system;
        ApprovalHost.Content = _approval;
        SecurityHost.Content = new SecurityPage(_api, () => EndSession("请重新登录。"));
        var roles = result.Roles is not { Count: > 0 } ? "暂无角色" : string.Join(" · ", result.Roles.Select(x => x.Name));
        SessionName.Text = result.UserName;
        SessionRoles.Text = roles;
        SetNavUser(result.UserName ?? string.Empty);
        NavUserRoles.Text = roles;
        if (_rememberMe) CredentialStore.Save(_loginUserName, _loginPassword);
        else CredentialStore.Clear();
        _loginPassword = string.Empty;
        Show(SessionPanel);
        MainNav.Select("home");
        await LoadThemePreferenceAsync();
        await LoadWorkspaceAsync();
    }

    private async Task LoadThemePreferenceAsync()
    {
        try
        {
            var preference = await _api.GetMyPreferencesAsync();
            if (preference is null) return;
            _themeFromServer = true;
            var index = Array.IndexOf(ThemeManager.Ids, preference.ThemeStyle);
            var dark = preference.ThemeMode switch { "dark" => true, "light" => false, _ => _darkMode };
            SetThemeState(index >= 0 ? index : _themeStyle, dark);
        }
        catch (SessionExpiredException) { throw; }
        catch (Exception) { }
        finally { _themeFromServer = false; }
    }

    private async void RefreshClick(object sender, RoutedEventArgs e) => await RunAsync(LoadWorkspaceAsync);

    private async Task LoadWorkspaceAsync()
    {
        var profile = await _api.GetMyProfileAsync();
        ApplyProfile(profile);
        await LoadNavAvatarAsync(profile.Avatar);

        string pendingText;
        try
        {
            var pending = await _api.GetMyPendingAsync();
            pendingText = $"共 {pending.TotalCount} 项待办";
        }
        catch (SessionExpiredException) { throw; }
        catch (Exception) { pendingText = "待办暂不可用"; }

        try
        {
            var notifications = await _api.GetNotificationsAsync();
            NotificationGrid.ItemsSource = notifications.Items;
            UnreadCount.Text = $"{notifications.UnreadCount} 条未读";
            SummaryText.Text = $"{pendingText}  ·  {UnreadCount.Text}";
        }
        catch (SessionExpiredException) { throw; }
        catch (Exception)
        {
            UnreadCount.Text = "通知暂不可用";
            NotificationGrid.ItemsSource = null;
            SummaryText.Text = pendingText;
        }
    }

    private void ApplyProfile(UserProfile profile)
    {
        ProfileUserName.Text = profile.UserName;
        ProfileAvatar.SetAvatar(profile.Avatar);
        ProfileNickName.Text = profile.NickName ?? string.Empty;
        ProfileName.Text = profile.Name ?? string.Empty;
        ProfileDescription.Text = profile.Description ?? string.Empty;
        ProfileContact.Text = $"邮箱：{profile.Email ?? "未设置"}    手机：{profile.Phone ?? "未设置"}";
        var displayName = string.IsNullOrWhiteSpace(profile.NickName) ? profile.UserName : profile.NickName;
        SessionName.Text = $"你好，{displayName}";
        SetNavUser(displayName);
        if (profile.Roles is { Count: > 0 })
        {
            var roles = string.Join(" · ", profile.Roles.Select(x => x.Name));
            SessionRoles.Text = roles;
            NavUserRoles.Text = roles;
        }
    }

    private void SetNavUser(string displayName)
    {
        NavUserName.Text = displayName;
        NavAvatarText.Text = string.IsNullOrWhiteSpace(displayName) ? "?" : displayName.Trim()[..1].ToUpperInvariant();
        NavAvatarText.Visibility = Visibility.Visible;
        NavAvatarFrame.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "SoftBrush");
    }

    private async Task ReloadNavAvatarAsync(string? avatar)
    {
        try { await LoadNavAvatarAsync(avatar); }
        catch (SessionExpiredException) { EndSession("登录已失效，请重新登录。"); }
    }

    private async Task LoadNavAvatarAsync(string? avatar)
    {
        if (string.IsNullOrWhiteSpace(avatar)) return;
        try
        {
            var bytes = await _api.DownloadContentFileAsync(avatar);
            var bitmap = new BitmapImage();
            using (var stream = new MemoryStream(bytes))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            bitmap.Freeze();
            NavAvatarFrame.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            NavAvatarText.Visibility = Visibility.Collapsed;
        }
        catch (SessionExpiredException) { throw; }
        catch (Exception)
        {
            // 头像加载失败时保留首字头像。
        }
    }

    private async void SaveProfileClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var nickName = ProfileNickName.Text.Trim();
        if (nickName.Length is < 1 or > 20) throw new InvalidOperationException("昵称须为 1–20 个字符。");
        var profile = await _api.UpdateMyProfileAsync(nickName, ProfileName.Text.Trim(), ProfileDescription.Text.Trim());
        ApplyProfile(profile);
    });

    private async void MarkReadClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (NotificationGrid.SelectedItem is not UserNotification item)
            throw new InvalidOperationException("请先选择一条通知。");
        await _api.MarkNotificationReadAsync(item.Id);
        await LoadWorkspaceAsync();
    });

    private async void MarkAllReadClick(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        await _api.MarkAllNotificationsReadAsync();
        await LoadWorkspaceAsync();
    });

    private void Show(UIElement panel)
    {
        if (panel == LoginPanel) _signedIn = false;
        var session = panel == SessionPanel;
        LoginPanel.Visibility = panel == LoginPanel ? Visibility.Visible : Visibility.Collapsed;
        FactorPanel.Visibility = panel == FactorPanel ? Visibility.Visible : Visibility.Collapsed;
        SessionPanel.Visibility = session ? Visibility.Visible : Visibility.Collapsed;
        LoginBody.Visibility = session ? Visibility.Collapsed : Visibility.Visible;
        HeaderBar.Visibility = session ? Visibility.Visible : Visibility.Collapsed;
        HeaderRow.Height = session ? GridLength.Auto : new GridLength(0);
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        LoginButton.IsEnabled = false;
        try { await action(); }
        catch (Exception error)
        {
            if (error is SessionExpiredException) EndSession("登录已失效，请重新登录。");
            else CustomMessage.TopError(this, error is TaskCanceledException
                ? "连接超时，请检查 appsettings.json 中的 API 地址。"
                : error.Message);
        }
        finally { _busy = false; LoginButton.IsEnabled = true; }
    }

    private void EndSession(string? message)
    {
        _signedIn = false;
        ClearSessionContent();
        Show(LoginPanel);
        PrefillRememberedLogin();
        if (!string.IsNullOrWhiteSpace(message)) CustomMessage.TopError(this, message);
    }

    private void PrefillRememberedLogin()
    {
        var saved = CredentialStore.Load();
        if (saved is null)
        {
            RememberMe.IsChecked = false;
            Password.Clear();
            return;
        }
        UserName.Text = saved.Value.UserName;
        Password.Password = saved.Value.Password;
        RememberMe.IsChecked = true;
    }

    private void ClearSessionContent()
    {
        foreach (var tab in _businessTabs.Values) WorkspaceTabs.Items.Remove(tab);
        _businessTabs.Clear();
        _businessWorkspaces.Clear();
        _sessionUserName = string.Empty;
        PersonalMenuPopup.IsOpen = false;
        SecurityHost.Content = null;
        AuthorizationHost.Content = null;
        SystemHost.Content = null;
        ApprovalHost.Content = null;
        _authorization = null;
        _system = null;
        _approval = null;
        NotificationGrid.ItemsSource = null;
        UnreadCount.Text = "0 条未读";
        SummaryText.Text = string.Empty;
        SessionName.Text = string.Empty;
        SessionRoles.Text = string.Empty;
        SetNavUser(string.Empty);
        NavUserRoles.Text = string.Empty;
    }
}
