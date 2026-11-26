using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Sim.Admin.Wpf.Client.Controls.Overlay;

/// <summary>浮层控件的公共构造逻辑：统一主题背景、前景、字体与无投影边框。</summary>
internal static class CustomOverlayBuilder
{
    public const string FontFamilyValue = "Cascadia Code,Microsoft YaHei UI,Consolas";

    /// <summary>创建带主题的窗口基础设置。</summary>
    public static T CreateWindow<T>(Window? owner, string title, double width) where T : Window, new()
    {
        var window = new T
        {
            Title = title,
            Width = width,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            FontFamily = new FontFamily(FontFamilyValue)
        };
        window.SetResourceReference(Window.BackgroundProperty, "PageBrush");
        window.SetResourceReference(Window.ForegroundProperty, "InkBrush");

        if (owner is not null)
        {
            window.Owner = owner;
        }

        return window;
    }

    /// <summary>标题行：加粗 18 号文字。</summary>
    public static TextBlock CreateTitle(string title) => new()
    {
        Text = title,
        FontSize = 18,
        FontWeight = FontWeights.Bold,
        TextWrapping = TextWrapping.Wrap
    };

    /// <summary>2px 描边分隔线，不使用投影。</summary>
    public static Border CreateDivider()
    {
        var divider = new Border { Height = 2, Margin = new Thickness(0, 12, 0, 12) };
        divider.SetResourceReference(Border.BorderBrushProperty, "EdgeBrush");
        divider.SetResourceReference(Border.BackgroundProperty, "EdgeBrush");
        return divider;
    }

    /// <summary>右下角对齐的操作按钮行。</summary>
    public static StackPanel CreateActions(CustomButton confirm, CustomButton cancel)
    {
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        cancel.Margin = new Thickness(0, 0, 8, 0);
        actions.Children.Add(cancel);
        actions.Children.Add(confirm);
        return actions;
    }

    public static CustomButton CreateButton(string text, CustomButtonVariant variant) => new()
    {
        Content = text,
        Variant = variant,
        MinWidth = 88
    };

    public static Color BrushToColor(Brush brush) =>
        brush is SolidColorBrush solid ? solid.Color : Colors.Transparent;

    public static Brush ResolveBrush(FrameworkElement element, string key) =>
        element.TryFindResource(key) as Brush ?? Brushes.Transparent;

    public static ToggleButton CreateCloseButton() => new()
    {
        Content = "×",
        Width = 30,
        Height = 30
    };
}
