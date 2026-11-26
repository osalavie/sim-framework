using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sim.Admin.Wpf.Client.Controls.Inputs;

/// <summary>Naive UI 的 n-input：单行文本输入，支持占位提示、清除按钮和校验态。</summary>
public sealed class CustomInput : TextBox
{
    /// <summary>清除当前文本的路由命令，供模板中的清除按钮使用。</summary>
    public static readonly RoutedCommand ClearCommand = new("Clear", typeof(CustomInput));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(CustomInput), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomInput), new PropertyMetadata(CustomControlSize.Medium));

    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(CustomInput), new PropertyMetadata(false));

    public static readonly DependencyProperty AllowClearProperty = DependencyProperty.Register(
        nameof(AllowClear), typeof(bool), typeof(CustomInput), new PropertyMetadata(false));

    public CustomInput()
    {
        CommandBindings.Add(new CommandBinding(ClearCommand, OnClearExecuted, OnClearCanExecute));
    }

    /// <summary>占位提示，文本为空时显示。</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>控件尺寸，默认 Medium（36 像素高）。</summary>
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

    /// <summary>是否显示右侧清除按钮。</summary>
    public bool AllowClear
    {
        get => (bool)GetValue(AllowClearProperty);
        set => SetValue(AllowClearProperty, value);
    }

    /// <summary>清空文本内容（TextBox 基类已提供，此处保留显式契约）。</summary>
    public new void Clear() => base.Clear();

    private void OnClearExecuted(object sender, ExecutedRoutedEventArgs e) => Clear();

    private void OnClearCanExecute(object sender, CanExecuteRoutedEventArgs e) =>
        e.CanExecute = !string.IsNullOrEmpty(Text);
}
