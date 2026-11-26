using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.SystemManagement;

public partial class StoragePage : UserControl
{
    private readonly ApiClient _api;
    private readonly Action? _sessionExpired;
    private List<StorageProviderState> _providers = [];
    private StorageProviderState? _editing;
    private bool _busy;
    private bool _loaded;

    public StoragePage(ApiClient api, Action? sessionExpired = null)
    {
        _api = api;
        _sessionExpired = sessionExpired;
        InitializeComponent();
        DefaultProvider.DisplayMemberPath = nameof(StorageProviderState.Title);
        DefaultProvider.SelectedValuePath = nameof(StorageProviderState.Provider);
        ProviderList.DisplayMemberPath = nameof(StorageProviderState.Title);
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
        var configuration = await _api.GetStorageConfigurationAsync();
        _providers = configuration.Providers.Select(provider => new StorageProviderState
        {
            Provider = provider.Provider ?? string.Empty,
            Enabled = provider.Enabled,
            RootPath = provider.RootPath,
            Endpoint = provider.Endpoint,
            Region = provider.Region,
            Bucket = provider.Bucket,
            AccessKeyId = provider.AccessKeyId,
            HasAccessKeySecret = provider.HasAccessKeySecret,
            HasSessionToken = provider.HasSessionToken,
            ForcePathStyle = provider.ForcePathStyle
        }).ToList();
        if (_providers.Count == 0)
            _providers.Add(new StorageProviderState { Provider = "local", Enabled = true });
        ProviderList.ItemsSource = _providers;
        DefaultProvider.ItemsSource = _providers;
        DefaultProvider.SelectedValue = configuration.DefaultProvider ?? _providers[0].Provider;
        MaxUploadMb.Text = Math.Max(1, Math.Round(configuration.MaxUploadBytes / 1024d / 1024d)).ToString();
        _editing = null;
        ProviderList.SelectedIndex = 0;
        Status.Text = $"共 {_providers.Count} 个存储提供方";
        UpdateButtons();
    }

    private async Task LoadAsync() => await RunAsync(LoadCoreAsync);

    private void ProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        CommitEditor();
        _editing = ProviderList.SelectedItem as StorageProviderState;
        BindEditor();
        UpdateButtons();
    }

    private void BindEditor()
    {
        if (_editing is null)
        {
            ProviderTitle.Text = "请选择提供方";
            LocalFields.Visibility = Visibility.Collapsed;
            ObjectFields.Visibility = Visibility.Collapsed;
            return;
        }
        ProviderTitle.Text = ProviderLabel(_editing.Provider);
        EnabledCheck.IsChecked = _editing.Enabled;
        var local = _editing.Provider == "local";
        LocalFields.Visibility = local ? Visibility.Visible : Visibility.Collapsed;
        ObjectFields.Visibility = local ? Visibility.Collapsed : Visibility.Visible;
        RootPath.Text = _editing.RootPath ?? string.Empty;
        Endpoint.Text = _editing.Endpoint ?? string.Empty;
        Region.Text = _editing.Region ?? string.Empty;
        Bucket.Text = _editing.Bucket ?? string.Empty;
        AccessKeyId.Text = _editing.AccessKeyId ?? string.Empty;
        AccessKeySecret.Password = string.Empty;
        SessionToken.Password = string.Empty;
        ForcePathStyle.IsChecked = _editing.ForcePathStyle;
        ClearSecrets.IsChecked = false;
        SecretLabel.Text = _editing.HasAccessKeySecret
            ? "Access Key Secret · 已配置，留空保持不变"
            : "Access Key Secret *";
        TokenLabel.Text = _editing.HasSessionToken
            ? "Session Token · 已配置，留空保持不变"
            : "Session Token";
    }

    private void CommitEditor()
    {
        if (_editing is null) return;
        _editing.Enabled = EnabledCheck.IsChecked == true;
        _editing.RootPath = RootPath.Text.Trim();
        _editing.Endpoint = EmptyToNull(Endpoint.Text);
        _editing.Region = EmptyToNull(Region.Text);
        _editing.Bucket = EmptyToNull(Bucket.Text);
        _editing.AccessKeyId = EmptyToNull(AccessKeyId.Text);
        if (AccessKeySecret.Password.Length > 0) _editing.Secret = AccessKeySecret.Password;
        if (SessionToken.Password.Length > 0) _editing.Token = SessionToken.Password;
        _editing.ClearSecrets = ClearSecrets.IsChecked == true;
        _editing.ForcePathStyle = ForcePathStyle.IsChecked == true;
    }

    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        CommitEditor();
        if (!TryReadMaxUploadMb(out var maxUploadBytes)) return;
        var defaultProvider = DefaultProvider.SelectedValue as string;
        if (string.IsNullOrWhiteSpace(defaultProvider))
        {
            Status.Text = "请选择默认提供方。";
            return;
        }
        foreach (var provider in _providers.Where(provider => provider.Enabled))
        {
            if (!Validate(provider, out var error))
            {
                Status.Text = $"{ProviderLabel(provider.Provider)}：{error}";
                ProviderList.SelectedItem = provider;
                return;
            }
        }
        await RunAsync(async () =>
        {
            var update = new StorageConfigurationUpdate
            {
                DefaultProvider = defaultProvider,
                MaxUploadBytes = maxUploadBytes,
                Providers = _providers.Select(provider => new StorageProviderUpdate
                {
                    Provider = provider.Provider,
                    Enabled = provider.Enabled,
                    RootPath = provider.RootPath,
                    Endpoint = provider.Endpoint,
                    Region = provider.Region,
                    Bucket = provider.Bucket,
                    AccessKeyId = provider.AccessKeyId,
                    AccessKeySecret = string.IsNullOrWhiteSpace(provider.Secret) ? null : provider.Secret,
                    ClearAccessKeySecret = provider.ClearSecrets,
                    SessionToken = string.IsNullOrWhiteSpace(provider.Token) ? null : provider.Token,
                    ClearSessionToken = provider.ClearSecrets,
                    ForcePathStyle = provider.ForcePathStyle
                }).ToList()
            };
            await _api.UpdateStorageConfigurationAsync(update);
            await LoadCoreAsync();
            Status.Text = "存储配置已保存";
        });
    }

    private async void TestClick(object sender, RoutedEventArgs e)
    {
        CommitEditor();
        if (_editing is not { } provider) return;
        if (!Validate(provider, out var error))
        {
            Status.Text = $"{ProviderLabel(provider.Provider)}：{error}";
            return;
        }
        await RunAsync(async () =>
        {
            var result = await _api.TestStorageProviderAsync(new StorageTestRequest
            {
                Provider = provider.Provider,
                RootPath = provider.RootPath,
                Endpoint = provider.Endpoint,
                Region = provider.Region,
                Bucket = provider.Bucket,
                AccessKeyId = provider.AccessKeyId,
                AccessKeySecret = string.IsNullOrWhiteSpace(provider.Secret) ? null : provider.Secret,
                SessionToken = string.IsNullOrWhiteSpace(provider.Token) ? null : provider.Token,
                ForcePathStyle = provider.ForcePathStyle
            });
            if (result.Success)
            {
                Status.Text = "存储连接测试通过";
                return;
            }
            Status.Text = $"存储连接测试失败：{result.Message}";
            MessageBox.Show(Window.GetWindow(this), result.Message ?? "存储连接测试失败。", "连接测试",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    private bool Validate(StorageProviderState provider, out string error)
    {
        error = string.Empty;
        if (!provider.Enabled) return true;
        if (provider.Provider == "local")
        {
            if (string.IsNullOrWhiteSpace(provider.RootPath))
            {
                error = "启用本地存储时必须填写根路径。";
                return false;
            }
            return true;
        }
        if (string.IsNullOrWhiteSpace(provider.Region) ||
            string.IsNullOrWhiteSpace(provider.Bucket) ||
            string.IsNullOrWhiteSpace(provider.AccessKeyId) ||
            (!provider.HasAccessKeySecret && string.IsNullOrWhiteSpace(provider.Secret)))
        {
            error = "启用对象存储时必须填写区域、存储桶、Access Key ID 和 Secret。";
            return false;
        }
        if (provider.ClearSecrets && string.IsNullOrWhiteSpace(provider.Secret))
        {
            error = "已勾选清除密钥，请同时填写新的 Access Key Secret。";
            return false;
        }
        return true;
    }

    private bool TryReadMaxUploadMb(out long maxUploadBytes)
    {
        maxUploadBytes = 0;
        if (!long.TryParse(MaxUploadMb.Text.Trim(), out var maxMb) || maxMb is < 1 or > 5120)
        {
            Status.Text = "最大上传大小须为 1–5120 MB 之间的整数。";
            return false;
        }
        maxUploadBytes = maxMb * 1024 * 1024;
        return true;
    }

    private async void RefreshClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private void UpdateButtons() => IsEnabled = !_busy;

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

    private static string? EmptyToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string ProviderLabel(string? provider) => provider switch
    {
        "local" => "本地文件",
        "s3" => "S3 兼容",
        "tencent-cos" => "腾讯云 COS",
        "aliyun-oss" => "阿里云 OSS",
        _ => provider ?? "未知"
    };
}

public sealed class StorageProviderState
{
    public string Provider { get; set; } = string.Empty;
    public string Title => StoragePage.ProviderLabel(Provider);
    public bool Enabled { get; set; }
    public string? RootPath { get; set; }
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKeyId { get; set; }
    public bool HasAccessKeySecret { get; set; }
    public bool HasSessionToken { get; set; }
    public bool ForcePathStyle { get; set; }
    public string Secret { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public bool ClearSecrets { get; set; }
}
