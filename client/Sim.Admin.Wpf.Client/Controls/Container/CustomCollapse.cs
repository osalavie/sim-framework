using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Container;

/// <summary>Naive UI 的 n-collapse：折叠面板容器，子项为 <see cref="CustomCollapseItem"/>。</summary>
public sealed class CustomCollapse : ItemsControl
{
}

/// <summary>Naive UI 的 n-collapse-item：可展开的折叠项。</summary>
public sealed class CustomCollapseItem : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(CustomCollapseItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HeaderExtraProperty = DependencyProperty.Register(
        nameof(HeaderExtra), typeof(object), typeof(CustomCollapseItem), new PropertyMetadata(null));

    public static readonly DependencyProperty ExpandedProperty = DependencyProperty.Register(
        nameof(Expanded), typeof(bool), typeof(CustomCollapseItem),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object? HeaderExtra
    {
        get => GetValue(HeaderExtraProperty);
        set => SetValue(HeaderExtraProperty, value);
    }

    public bool Expanded
    {
        get => (bool)GetValue(ExpandedProperty);
        set => SetValue(ExpandedProperty, value);
    }
}
