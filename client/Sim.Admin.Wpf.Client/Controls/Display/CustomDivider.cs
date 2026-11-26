using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Display;

/// <summary>Naive UI 的 n-divider：水平或垂直分隔线，可带标题、可虚线。</summary>
public sealed class CustomDivider : Control
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(CustomDivider), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DashedProperty = DependencyProperty.Register(
        nameof(Dashed), typeof(bool), typeof(CustomDivider), new PropertyMetadata(false));

    public static readonly DependencyProperty VerticalProperty = DependencyProperty.Register(
        nameof(Vertical), typeof(bool), typeof(CustomDivider), new PropertyMetadata(false));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool Dashed
    {
        get => (bool)GetValue(DashedProperty);
        set => SetValue(DashedProperty, value);
    }

    public bool Vertical
    {
        get => (bool)GetValue(VerticalProperty);
        set => SetValue(VerticalProperty, value);
    }
}
