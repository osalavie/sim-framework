using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Sim.Admin.Wpf.Client.Controls.Overlay;

/// <summary>消息提示的停靠位置；登录错误使用窗口顶部居中，其余沿用右下角堆叠。</summary>
public enum CustomMessagePlacement
{
    BottomRight,
    TopCenter
}

/// <summary>轻量消息提示（toast），对应 Naive UI 的 useMessage；不占用主窗口布局。</summary>
public static class CustomMessage
{
    private sealed record Entry(Window Window, CustomMessagePlacement Placement);

    private static readonly List<Entry> Active = [];
    private static readonly object Gate = new();
    private static readonly IntPtr TopmostHandle = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    public static void Show(
        Window? owner,
        string text,
        CustomTone tone = CustomTone.Default,
        double seconds = 2.5,
        CustomMessagePlacement placement = CustomMessagePlacement.BottomRight)
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            FontFamily = new FontFamily(CustomOverlayBuilder.FontFamilyValue)
        };
        if (owner is not null) window.Owner = owner;
        window.SetResourceReference(Window.ForegroundProperty, "InkBrush");

        var border = new Border
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 8, 14, 8)
        };
        border.SetResourceReference(Border.BackgroundProperty, "PaperBrush");
        border.SetResourceReference(Border.BorderBrushProperty, Controls.Display.CustomToneResources.BrushKey(tone));

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var symbol = new TextBlock
        {
            Text = Controls.Display.CustomToneResources.Symbol(tone),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        symbol.SetResourceReference(TextBlock.ForegroundProperty, Controls.Display.CustomToneResources.BrushKey(tone));
        row.Children.Add(symbol);
        row.Children.Add(new TextBlock
        {
            Text = text,
            MaxWidth = 360,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });
        border.Child = row;
        window.Content = border;

        lock (Gate)
        {
            Active.Add(new Entry(window, placement));
        }

        window.Opacity = 0;
        window.Show();
        Reposition(owner);
        window.ContentRendered += (_, _) =>
        {
            Reposition(owner);
            window.Opacity = 1;
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(0.5, seconds)) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            window.Close();
        };
        timer.Start();

        window.Closed += (_, _) =>
        {
            lock (Gate)
            {
                Active.RemoveAll(entry => ReferenceEquals(entry.Window, window));
            }

            Reposition(owner);
        };
    }

    public static void Success(Window? owner, string text) => Show(owner, text, CustomTone.Success);

    public static void Info(Window? owner, string text) => Show(owner, text, CustomTone.Info);

    public static void Warning(Window? owner, string text) => Show(owner, text, CustomTone.Warning);

    public static void Error(Window? owner, string text) => Show(owner, text, CustomTone.Error);

    /// <summary>窗口顶部居中的错误提示，用于登录失败等需要立即注意的消息。</summary>
    public static void TopError(Window? owner, string text, double seconds = 3.5) =>
        Show(owner, text, CustomTone.Error, seconds, CustomMessagePlacement.TopCenter);

    /// <summary>窗口顶部居中的信息提示。</summary>
    public static void TopInfo(Window? owner, string text, double seconds = 2.5) =>
        Show(owner, text, CustomTone.Info, seconds, CustomMessagePlacement.TopCenter);

    /// <summary>按物理像素相对 owner 窗口定位；WPF 的 Left/Top 在多显示器混合 DPI 下会偏移。</summary>
    private static void Reposition(Window? owner)
    {
        var anchor = owner ?? Application.Current?.MainWindow;
        if (anchor is null) return;
        var anchorHandle = new WindowInteropHelper(anchor).Handle;
        if (anchorHandle == IntPtr.Zero || !GetWindowRect(anchorHandle, out var anchorRect)) return;
        var areaWidth = anchorRect.Right - anchorRect.Left;

        lock (Gate)
        {
            var bottomOffset = 16;
            var topOffset = 16;
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var entry = Active[i];
                var handle = new WindowInteropHelper(entry.Window).Handle;
                if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect)) continue;
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                int x;
                int y;
                if (entry.Placement == CustomMessagePlacement.TopCenter)
                {
                    x = anchorRect.Left + (areaWidth - width) / 2;
                    y = anchorRect.Top + topOffset;
                    topOffset += height + 10;
                }
                else
                {
                    x = anchorRect.Right - width - 16;
                    y = anchorRect.Bottom - bottomOffset - height;
                    bottomOffset += height + 10;
                }
                SetWindowPos(handle, TopmostHandle, x, y, 0, 0, SwpNoSize | SwpNoActivate);
            }
        }
    }
}
