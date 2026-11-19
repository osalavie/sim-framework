using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Container;

/// <summary>Naive UI 的 n-tabs：顶部标签条 + 内容区，标签样式由样式字典注入。</summary>
public sealed class CustomTabs : TabControl
{
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomTabs),
        new PropertyMetadata(CustomControlSize.Medium));

    public CustomControlSize Size
    {
        get => (CustomControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
