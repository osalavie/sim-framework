using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Inputs;

/// <summary>Naive UI 的 n-form-item：纵向标签、内容与校验反馈。</summary>
public sealed class CustomFormItem : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(CustomFormItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RequiredProperty = DependencyProperty.Register(
        nameof(Required), typeof(bool), typeof(CustomFormItem), new PropertyMetadata(false));

    public static readonly DependencyProperty FeedbackProperty = DependencyProperty.Register(
        nameof(Feedback), typeof(string), typeof(CustomFormItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(CustomFormItem), new PropertyMetadata(false));

    public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(
        nameof(LabelWidth), typeof(double), typeof(CustomFormItem), new PropertyMetadata(0d));

    /// <summary>标签文本，为空时隐藏标签行。</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>是否必填，为 True 时在标签后追加 DangerBrush 的星号。</summary>
    public bool Required
    {
        get => (bool)GetValue(RequiredProperty);
        set => SetValue(RequiredProperty, value);
    }

    /// <summary>校验反馈文本，非空时显示在内容下方。</summary>
    public string Feedback
    {
        get => (string)GetValue(FeedbackProperty);
        set => SetValue(FeedbackProperty, value);
    }

    /// <summary>校验失败态，为 True 时反馈文本使用 DangerBrush。</summary>
    public bool IsInvalid
    {
        get => (bool)GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    /// <summary>标签最小宽度，0 表示按内容自适应。</summary>
    public double LabelWidth
    {
        get => (double)GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }
}
