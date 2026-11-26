using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Overlay;

/// <summary>侧边抽屉，对应 Naive UI 的 n-drawer；非模态，停靠在主窗口右侧。</summary>
public static class CustomDrawer
{
    /// <summary>打开抽屉，返回已创建的窗口实例便于关闭或更新内容。</summary>
    public static Window Show(Window? owner, string title, UIElement content, double width = 440)
    {
        var window = CustomOverlayBuilder.CreateWindow<Window>(owner, title, width);
        window.MinWidth = 320;
        window.MinHeight = 320;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(CustomOverlayBuilder.CreateTitle(title));
        body.Children.Add(CustomOverlayBuilder.CreateDivider());
        body.Children.Add(content);
        window.Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body
        };

        window.Loaded += (_, _) => Position(window, owner);
        owner?.LocationChanged += (_, _) => Position(window, owner);
        owner?.SizeChanged += (_, _) => Position(window, owner);
        window.Show();
        return window;
    }

    private static void Position(Window window, Window? owner)
    {
        if (owner is null || double.IsNaN(owner.Left))
        {
            return;
        }

        window.Left = Math.Max(0, owner.Left + owner.ActualWidth - window.Width);
        window.Top = owner.Top;
        window.Height = Math.Max(window.MinHeight, owner.ActualHeight);
    }
}
