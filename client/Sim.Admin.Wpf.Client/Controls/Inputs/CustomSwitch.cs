using System.Windows;
using System.Windows.Controls.Primitives;

namespace Sim.Admin.Wpf.Client.Controls.Inputs;

/// <summary>Naive UI 的 n-switch：二元开关，轨道与滑块随尺寸变化。</summary>
public sealed class CustomSwitch : ToggleButton
{
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomSwitch), new PropertyMetadata(CustomControlSize.Medium));

    /// <summary>控件尺寸，默认 Medium（40 × 22 轨道）。</summary>
    public CustomControlSize Size
    {
        get => (CustomControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
