using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Inputs;

/// <summary>Naive UI 的 n-input type="textarea"：自动换行、可滚动、按尺寸控制最小高度。</summary>
public sealed class CustomTextarea : TextBox
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(CustomTextarea), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomTextarea), new PropertyMetadata(CustomControlSize.Medium));

    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(CustomTextarea), new PropertyMetadata(false));

    public CustomTextarea()
    {
        AcceptsReturn = true;
        TextWrapping = TextWrapping.Wrap;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    /// <summary>占位提示，文本为空时显示。</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>控件尺寸，默认 Medium（最小高度 88）。</summary>
    public CustomControlSize Size
    {
        get => (CustomControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>校验失败态，描边切换为 DangerBrush。</summary>
    public bool IsInvalid
    {
        get => (bool)GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }
}
