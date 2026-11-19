using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sim.DeviceAgent.Configuration;

namespace Sim.DeviceAgent.Tray;

public sealed class TrayService : IHostedService
{
    private readonly AgentConfig _config;
    private readonly AgentConfigFile _file;
    private readonly AgentConfigReloader _reloader;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TrayService> _logger;
    private TrayApplicationContext? _context;
    private Thread? _thread;

    public TrayService(
        AgentConfig config,
        AgentConfigFile file,
        AgentConfigReloader reloader,
        IHostApplicationLifetime lifetime,
        ILogger<TrayService> logger)
    {
        _config = config;
        _file = file;
        _reloader = reloader;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_config.Agent.Tray.Enabled)
        {
            _logger.LogInformation("Windows tray is disabled by agent.tray.enabled=false.");
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            _logger.LogInformation("Windows tray is unavailable on this operating system.");
            return;
        }

        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new TrayApplicationContext(
            _config,
            _file,
            _reloader,
            _lifetime,
            message => _logger.LogInformation("{Message}", message),
            message => _logger.LogWarning("{Message}", message));

        _context = context;
        _thread = new Thread(() => RunTrayThread(context, ready))
        {
            IsBackground = true,
            Name = "DeviceAgentTray"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        try
        {
            if (await ready.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Windows tray icon started.");
            }
            else
            {
                _logger.LogWarning("Windows tray could not be started; the agent continues without a tray icon.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Windows tray did not start within 3 seconds; the agent continues without a tray icon.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _context?.RequestExit();

        if (_thread is { IsAlive: true } && Thread.CurrentThread != _thread)
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        return Task.CompletedTask;
    }

    private void RunTrayThread(TrayApplicationContext context, TaskCompletionSource<bool> ready)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            context.Create();
            ready.TrySetResult(true);
            Application.Run(context);
        }
        catch (Exception ex)
        {
            ready.TrySetResult(false);
            _logger.LogWarning(ex, "Windows tray failed.");
            context.RequestExit();
        }
    }

    private sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly AgentConfig _config;
        private readonly AgentConfigFile _file;
        private readonly AgentConfigReloader _reloader;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarning;
        private readonly ConcurrentQueue<string> _notifications = new();

        private NotifyIcon? _icon;
        private ContextMenuStrip? _menu;
        private System.Windows.Forms.Timer? _timer;
        private volatile bool _exitRequested;

        public TrayApplicationContext(
            AgentConfig config,
            AgentConfigFile file,
            AgentConfigReloader reloader,
            IHostApplicationLifetime lifetime,
            Action<string> logInfo,
            Action<string> logWarning)
        {
            _config = config;
            _file = file;
            _reloader = reloader;
            _lifetime = lifetime;
            _logInfo = logInfo;
            _logWarning = logWarning;
        }

        public void Create()
        {
            _menu = new ContextMenuStrip();
            var header = new ToolStripMenuItem($"Device Agent · {_config.Agent.Id}")
            {
                Enabled = false
            };
            _menu.Items.Add(header);
            _menu.Items.Add(new ToolStripSeparator());

            var openStatus = new ToolStripMenuItem("打开状态");
            openStatus.Click += (_, _) => OpenStatus();
            _menu.Items.Add(openStatus);

            var openConfig = new ToolStripMenuItem("打开配置文件");
            openConfig.Click += (_, _) => OpenConfig();
            _menu.Items.Add(openConfig);

            var reload = new ToolStripMenuItem("重新加载配置");
            reload.Click += (_, _) => RequestReload();
            _menu.Items.Add(reload);

            _menu.Items.Add(new ToolStripSeparator());
            var exit = new ToolStripMenuItem("退出");
            exit.Click += (_, _) =>
            {
                _exitRequested = true;
                _lifetime.StopApplication();
            };
            _menu.Items.Add(exit);

            var tooltip = $"Device Agent · {_config.Agent.Id}";
            _icon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = tooltip.Length > 63 ? tooltip[..63] : tooltip,
                Visible = true,
                ContextMenuStrip = _menu
            };
            _icon.DoubleClick += (_, _) => OpenStatus();

            _timer = new System.Windows.Forms.Timer { Interval = 300 };
            _timer.Tick += (_, _) => OnTimerTick();
            _timer.Start();

            _notifications.Enqueue(
                $"已启动：ws://{_config.Agent.LocalWs.Bind}:{_config.Agent.LocalWs.Port}/ws");
        }

        public void RequestExit() => _exitRequested = true;

        protected override void ExitThreadCore()
        {
            try
            {
                _timer?.Stop();
                _timer?.Dispose();
                _timer = null;

                if (_icon is not null)
                {
                    _icon.Visible = false;
                    _icon.Dispose();
                    _icon = null;
                }

                _menu?.Dispose();
                _menu = null;
            }
            finally
            {
                base.ExitThreadCore();
            }
        }

        private void OnTimerTick()
        {
            if (_exitRequested)
            {
                _timer?.Stop();
                ExitThread();
                return;
            }

            while (_notifications.TryDequeue(out var message))
            {
                ShowBalloon("Device Agent", message);
            }
        }

        private void RequestReload()
        {
            ShowBalloon("Device Agent", "正在重新加载配置…");
            _logInfo("Tray requested a configuration reload.");

            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _reloader.ReloadAsync(CancellationToken.None).ConfigureAwait(false);
                    _notifications.Enqueue(result.Success
                        ? $"配置已重新加载。{result.Message}"
                        : $"配置重载失败：{result.Message}");
                }
                catch (Exception ex)
                {
                    _notifications.Enqueue($"配置重载失败：{ex.Message}");
                    _logWarning($"Tray reload failed: {ex.Message}");
                }
            });
        }

        private void OpenStatus()
        {
            var url = $"http://{_config.Agent.LocalWs.Bind}:{_config.Agent.LocalWs.Port}/api/status";
            OpenWithShell(url);
        }

        private void OpenConfig() => OpenWithShell(_file.Path);

        private void OpenWithShell(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logWarning($"Failed to open '{target}': {ex.Message}");
                _notifications.Enqueue($"无法打开：{target}");
            }
        }

        private void ShowBalloon(string title, string message)
        {
            try
            {
                _icon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                _logWarning($"Tray balloon failed: {ex.Message}");
            }
        }
    }
}
