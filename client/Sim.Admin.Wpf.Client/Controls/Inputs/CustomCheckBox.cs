using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Inputs;

/// <summary>Naive UI 的 n-checkbox：18 像素方框加右侧标签，选中时填充 PrimaryBrush。</summary>
public sealed class CustomCheckBox : CheckBox
{
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomCheckBox), new PropertyMetadata(CustomControlSize.Medium));

    /// <summary>控件尺寸，默认 Medium（方框 18 像素，Small/Large 为 16/20）。</summary>
    public CustomControlSize Size
    {
        get => (CustomControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
