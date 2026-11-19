using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Inputs;

/// <summary>Naive UI 的 n-radio：圆形描边加右侧标签，选中时显示 PrimaryBrush 内圆点。</summary>
public sealed class CustomRadio : RadioButton
{
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomRadio), new PropertyMetadata(CustomControlSize.Medium));

    /// <summary>控件尺寸，默认 Medium（圆形 18 像素，Small/Large 为 16/20）。</summary>
    public CustomControlSize Size
    {
        get => (CustomControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
