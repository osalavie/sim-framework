using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Sim.DeviceAgent.App.Shell;
using Sim.DeviceAgent.App.Tray;
using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Hosting;

namespace Sim.DeviceAgent.App;

public partial class App : Application
{
    private const string InstanceName = "Sim.DeviceAgent.App";

    private MainWindow? _window;
    private AgentRuntimeHost? _host;
    private TrayIcon? _tray;
    private SingleInstance? _singleInstance;
    private Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue = null!;
    private string _configPath = string.Empty;
    private bool _exiting;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        var commandLine = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var autostart = commandLine.Any(
            arg => string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase));

        _singleInstance = new SingleInstance(InstanceName);
        if (!_singleInstance.IsPrimary)
        {
            _singleInstance.SignalShow();
            Exit();
            return;
        }

        _singleInstance.StartServer(() => _dispatcherQueue.TryEnqueue(ShowMainWindow));

        _configPath = AgentConfigLoader.ResolveConfigPath(commandLine);
        var config = LoadConfig(out var configError);

        if (config is not null)
        {
            try
            {
                _host = AgentRuntimeHost.Create(config, _configPath, logging => logging.AddDebug());
            }
            catch (Exception ex)
            {
                configError = ex.Message;
            }
        }

        _window = new MainWindow(_host, _configPath, configError);

        var trayEnabled = config?.Agent.Tray.Enabled ?? true;
        if (trayEnabled)
        {
            _tray = new TrayIcon(
                config?.Agent.Id ?? "Device Agent",
                onShow: ShowMainWindow,
                onOpenConfig: OpenConfigFile,
                onReload: () => _ = ReloadAsync(),
                onExit: () => _ = ExitAsync());
            _tray.Initialize();
        }

        if (!autostart || !trayEnabled)
        {
            ShowMainWindow();
        }

        if (_host is not null)
        {
            _ = StartHostAsync();
        }
    }

    private AgentConfig? LoadConfig(out string? error)
    {
        error = null;
        try
        {
            var config = AgentConfigLoader.Load(_configPath);
            var errors = AgentConfigValidator.Validate(config);
            if (errors.Count > 0)
            {
                error = string.Join(Environment.NewLine, errors);
                return null;
            }

            return config;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private async Task StartHostAsync()
    {
        try
        {
            await _host!.StartAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _window?.ShowError($"运行时启动失败：{ex.Message}");
        }
    }

    private void ShowMainWindow()
    {
        _window?.ShowFromTray();
    }

    private void OpenConfigFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo(_configPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _tray?.ShowBalloon("Device Agent", $"无法打开配置文件：{ex.Message}");
        }
    }

    private async Task ReloadAsync()
    {
        if (_host is null)
        {
            _tray?.ShowBalloon("Device Agent", "当前没有可用的运行配置。");
            return;
        }

        try
        {
            var result = await _host.Reloader.ReloadAsync(CancellationToken.None);
            var message = result.Success ? "配置已重新加载。" : $"配置重载失败：{result.Message}";
            _dispatcherQueue.TryEnqueue(() => _tray?.ShowBalloon("Device Agent", message));
        }
        catch (Exception ex)
        {
            _dispatcherQueue.TryEnqueue(() => _tray?.ShowBalloon("Device Agent", $"配置重载失败：{ex.Message}"));
        }
    }

    private async Task ExitAsync()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _tray?.Dispose();
        _tray = null;

        if (_host is not null)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _host.StopAsync(timeout.Token);
            }
            catch
            {
                // The process is exiting; spool replay recovers unacknowledged data on restart.
            }

            await _host.DisposeAsync();
            _host = null;
        }

        _window?.AllowClose();
        _window?.Close();
        _singleInstance?.Dispose();
        Exit();
    }
}
