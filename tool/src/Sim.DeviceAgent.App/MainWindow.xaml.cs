using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Sim.DeviceAgent.Hosting;

namespace Sim.DeviceAgent.App;

public sealed partial class MainWindow : Window
{
    private readonly AgentRuntimeHost? _host;
    private readonly DispatcherQueueTimer _timer;
    private readonly ObservableCollection<string> _channels = [];
    private readonly ObservableCollection<string> _sinks = [];
    private bool _allowClose;

    public MainWindow(AgentRuntimeHost? host, string configPath, string? startupError)
    {
        InitializeComponent();

        _host = host;
        Title = "Device Agent";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(980, 620));
        AppWindow.Closing += OnClosing;
        ChannelList.ItemsSource = _channels;
        SinkList.ItemsSource = _sinks;
        ConfigText.Text = $"配置文件：{configPath}";

        if (startupError is not null)
        {
            ShowError(startupError);
        }

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    public void ShowFromTray()
    {
        if (!AppWindow.IsVisible)
        {
            AppWindow.Show();
        }

        Activate();
    }

    public void ShowError(string message)
    {
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
        StateText.Text = "配置或运行时错误";
    }

    public void AllowClose() => _allowClose = true;

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            _timer.Stop();
            return;
        }

        args.Cancel = true;
        AppWindow.Hide();
    }

    private void Refresh()
    {
        if (_host is null)
        {
            return;
        }

        var snapshot = _host.GetSnapshot();
        var displayName = string.IsNullOrWhiteSpace(snapshot.DisplayName)
            ? snapshot.AgentId
            : $"{snapshot.DisplayName}（{snapshot.AgentId}）";

        if (!ErrorBar.IsOpen)
        {
            StateText.Text = $"运行中 · ws://{snapshot.LocalWsBind}:{snapshot.LocalWsPort}/ws";
        }

        HeaderText.Text = $"Device Agent · {displayName}";
        ReloadText.Text = snapshot.Config.LastError is null
            ? snapshot.Config.LastMessage
            : $"最近错误：{snapshot.Config.LastError}";

        UpdateItems(
            _channels,
            snapshot.Channels.Select(channel =>
                $"{channel.DeviceName}（{channel.DeviceId}）· {channel.ChannelId} · {channel.State} · " +
                $"帧 {channel.ReceivedFrames} · 丢弃 {channel.DroppedFrames} · 解析错误 {channel.ParseErrors}"));

        UpdateItems(
            _sinks,
            snapshot.Sinks.Select(sink =>
                $"{sink.Id} · {sink.Type} · {sink.State} · 待发送 {sink.PendingEntries}"));
    }

    private static void UpdateItems(ObservableCollection<string> target, IEnumerable<string> values)
    {
        var items = values.ToArray();
        if (target.Count == items.Length && target.SequenceEqual(items))
        {
            return;
        }

        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
