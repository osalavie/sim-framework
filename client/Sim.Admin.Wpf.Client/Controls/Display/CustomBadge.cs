using System.Windows;

namespace Sim.Admin.Wpf.Client.Controls.Display;

/// <summary>Naive UI 的 n-badge：包裹内容并在右上角显示计数或圆点。</summary>
public sealed class CustomBadge : CustomTonedContentControl
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(CustomBadge),
        new PropertyMetadata(0, OnBadgeChanged));

    public static readonly DependencyProperty MaxProperty = DependencyProperty.Register(
        nameof(Max), typeof(int), typeof(CustomBadge),
        new PropertyMetadata(99, OnBadgeChanged));

    public static readonly DependencyProperty DotProperty = DependencyProperty.Register(
        nameof(Dot), typeof(bool), typeof(CustomBadge), new PropertyMetadata(false, OnBadgeChanged));

    public static readonly DependencyProperty ShowZeroProperty = DependencyProperty.Register(
        nameof(ShowZero), typeof(bool), typeof(CustomBadge), new PropertyMetadata(false, OnBadgeChanged));

    public static readonly DependencyProperty BadgeTextProperty = DependencyProperty.Register(
        nameof(BadgeText), typeof(string), typeof(CustomBadge), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HasBadgeProperty = DependencyProperty.Register(
        nameof(HasBadge), typeof(bool), typeof(CustomBadge), new PropertyMetadata(false));

    public CustomBadge() => RefreshBadge();

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int Max
    {
        get => (int)GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public bool Dot
    {
        get => (bool)GetValue(DotProperty);
        set => SetValue(DotProperty, value);
    }

    public bool ShowZero
    {
        get => (bool)GetValue(ShowZeroProperty);
        set => SetValue(ShowZeroProperty, value);
    }

    public string BadgeText => (string)GetValue(BadgeTextProperty);

    public bool HasBadge => (bool)GetValue(HasBadgeProperty);

    private static void OnBadgeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomBadge)d).RefreshBadge();

    private void RefreshBadge()
    {
        var show = Dot || Value > 0 || (ShowZero && Value == 0);
        SetCurrentValue(HasBadgeProperty, show);
        SetCurrentValue(BadgeTextProperty, Value > Max ? $"{Max}+" : Value.ToString());
    }
}
