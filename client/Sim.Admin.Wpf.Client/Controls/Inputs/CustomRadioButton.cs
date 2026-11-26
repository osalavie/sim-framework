using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Inputs;

/// <summary>Naive UI 的 n-radio-button：分段按钮样式，选中时使用 SoftBrush 背景与 PrimaryBrush 描边。</summary>
public sealed class CustomRadioButton : RadioButton
{
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomRadioButton), new PropertyMetadata(CustomControlSize.Medium));

    /// <summary>控件尺寸，默认 Medium（36 像素高）。</summary>
    public CustomControlSize Size
    {
        get => (CustomControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
