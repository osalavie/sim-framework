using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sim.Admin.Wpf.Client.Controls.Display;

/// <summary>Naive UI 的 n-progress：带语义色的进度条，可显示百分比。</summary>
public sealed class CustomProgress : ProgressBar
{
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(CustomTone), typeof(CustomProgress),
        new PropertyMetadata(CustomTone.Primary, OnToneChanged));

    public static readonly DependencyProperty ToneBrushProperty = DependencyProperty.Register(
        nameof(ToneBrush), typeof(Brush), typeof(CustomProgress), new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty ShowIndicatorProperty = DependencyProperty.Register(
        nameof(ShowIndicator), typeof(bool), typeof(CustomProgress), new PropertyMetadata(true));

    public static readonly DependencyProperty IndicatorTextProperty = DependencyProperty.Register(
        nameof(IndicatorText), typeof(string), typeof(CustomProgress), new PropertyMetadata("0%"));

    public CustomProgress()
    {
        Minimum = 0;
        Maximum = 100;
        Height = 8;
        RefreshTone();
        RefreshIndicatorText();
    }

    public CustomTone Tone
    {
        get => (CustomTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public Brush ToneBrush => (Brush)GetValue(ToneBrushProperty);

    public bool ShowIndicator
    {
        get => (bool)GetValue(ShowIndicatorProperty);
        set => SetValue(ShowIndicatorProperty, value);
    }

    public string IndicatorText => (string)GetValue(IndicatorTextProperty);

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        RefreshIndicatorText();
    }

    protected override void OnMaximumChanged(double oldValue, double newValue)
    {
        base.OnMaximumChanged(oldValue, newValue);
        RefreshIndicatorText();
    }

    protected override void OnMinimumChanged(double oldValue, double newValue)
    {
        base.OnMinimumChanged(oldValue, newValue);
        RefreshIndicatorText();
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomProgress)d).RefreshTone();

    private void RefreshTone()
    {
        SetResourceReference(ToneBrushProperty, CustomToneResources.BrushKey(Tone));
    }

    private void RefreshIndicatorText()
    {
        var range = Maximum - Minimum;
        var percent = range <= 0 ? 0 : Math.Clamp((Value - Minimum) / range * 100, 0, 100);
        SetCurrentValue(IndicatorTextProperty, $"{percent:0}%");
    }
}
