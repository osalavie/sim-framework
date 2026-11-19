using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls;

/// <summary>带语义色的文本，对应 Naive UI 的 n-text。</summary>
public sealed class CustomText : TextBlock
{
    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(CustomTextTone), typeof(CustomText),
        new PropertyMetadata(CustomTextTone.Default));

    public static readonly DependencyProperty StrongProperty = DependencyProperty.Register(
        nameof(Strong), typeof(bool), typeof(CustomText), new PropertyMetadata(false));

    public CustomTextTone Tone
    {
        get => (CustomTextTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool Strong
    {
        get => (bool)GetValue(StrongProperty);
        set => SetValue(StrongProperty, value);
    }
}
