using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Container;

/// <summary>Naive UI 的 n-timeline：纵向时间线容器，子项为 <see cref="CustomTimelineItem"/>。</summary>
public sealed class CustomTimeline : StackPanel
{
    public CustomTimeline()
    {
        Orientation = Orientation.Vertical;
    }
}

/// <summary>Naive UI 的 n-timeline-item：时间线节点，含语义色圆点与连接线。</summary>
public sealed class CustomTimelineItem : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(CustomTimelineItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time), typeof(string), typeof(CustomTimelineItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(CustomTone), typeof(CustomTimelineItem),
        new PropertyMetadata(CustomTone.Primary, OnToneChanged));

    public static readonly DependencyProperty IsLastProperty = DependencyProperty.Register(
        nameof(IsLast), typeof(bool), typeof(CustomTimelineItem), new PropertyMetadata(false));

    public static readonly DependencyProperty ToneBrushProperty = DependencyProperty.Register(
        nameof(ToneBrush), typeof(System.Windows.Media.Brush), typeof(CustomTimelineItem),
        new PropertyMetadata(System.Windows.Media.Brushes.Transparent));

    public CustomTimelineItem()
    {
        RefreshTone();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Time
    {
        get => (string)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    public CustomTone Tone
    {
        get => (CustomTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        set => SetValue(IsLastProperty, value);
    }

    public System.Windows.Media.Brush ToneBrush => (System.Windows.Media.Brush)GetValue(ToneBrushProperty);

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomTimelineItem)d).RefreshTone();

    private void RefreshTone() =>
        SetResourceReference(ToneBrushProperty,
            Controls.Display.CustomToneResources.BrushKey(Tone));
}
