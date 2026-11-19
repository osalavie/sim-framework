using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sim.Admin.Wpf.Client.Controls.Display;

/// <summary>语义色 → 主题资源的映射工具，供控件在 Tone 变化时动态绑定画笔。</summary>
public static class CustomToneResources
{
    public static string BrushKey(CustomTone tone) => tone switch
    {
        CustomTone.Primary => "PrimaryBrush",
        CustomTone.Info => "InfoBrush",
        CustomTone.Success => "SuccessBrush",
        CustomTone.Warning => "WarningBrush",
        CustomTone.Error => "DangerBrush",
        _ => "InkBrush"
    };

    public static string SoftBrushKey(CustomTone tone) => tone switch
    {
        CustomTone.Success => "SuccessSoftBrush",
        CustomTone.Warning => "WarningSoftBrush",
        CustomTone.Info => "InfoSoftBrush",
        CustomTone.Primary => "InfoSoftBrush",
        CustomTone.Error => "DangerSoftBrush",
        _ => "SoftBrush"
    };

    public static string Symbol(CustomTone tone) => tone switch
    {
        CustomTone.Success => "✓",
        CustomTone.Warning => "!",
        CustomTone.Error => "×",
        CustomTone.Info => "i",
        CustomTone.Primary => "✓",
        _ => "•"
    };

    /// <summary>为控件上的画笔属性绑定 Tone 对应的动态主题资源。</summary>
    public static void Apply(DependencyObject target, DependencyProperty brushProperty,
        DependencyProperty softProperty, CustomTone tone)
    {
        if (target is not FrameworkElement element)
        {
            return;
        }

        element.SetResourceReference(brushProperty, BrushKey(tone));
        element.SetResourceReference(softProperty, SoftBrushKey(tone));
    }

    public static Brush Resolve(FrameworkElement element, string key) =>
        element.TryFindResource(key) as Brush ?? Brushes.Transparent;
}

/// <summary>带 Tone 的 ContentControl 基类：Tag、Badge、Avatar、Alert、Result 共用。</summary>
public abstract class CustomTonedContentControl : ContentControl
{
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(CustomTone), typeof(CustomTonedContentControl),
        new PropertyMetadata(CustomTone.Default, OnToneChanged));

    public static readonly DependencyProperty ToneBrushProperty = DependencyProperty.Register(
        nameof(ToneBrush), typeof(Brush), typeof(CustomTonedContentControl),
        new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty ToneSoftBrushProperty = DependencyProperty.Register(
        nameof(ToneSoftBrush), typeof(Brush), typeof(CustomTonedContentControl),
        new PropertyMetadata(Brushes.Transparent));

    public static readonly DependencyProperty ToneSymbolProperty = DependencyProperty.Register(
        nameof(ToneSymbol), typeof(string), typeof(CustomTonedContentControl), new PropertyMetadata("•"));

    public CustomTone Tone
    {
        get => (CustomTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public Brush ToneBrush => (Brush)GetValue(ToneBrushProperty);

    public Brush ToneSoftBrush => (Brush)GetValue(ToneSoftBrushProperty);

    public string ToneSymbol => (string)GetValue(ToneSymbolProperty);

    protected CustomTonedContentControl()
    {
        RefreshTone();
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomTonedContentControl)d).RefreshTone();

    private void RefreshTone()
    {
        CustomToneResources.Apply(this, ToneBrushProperty, ToneSoftBrushProperty, Tone);
        SetCurrentValue(ToneSymbolProperty, CustomToneResources.Symbol(Tone));
    }
}
