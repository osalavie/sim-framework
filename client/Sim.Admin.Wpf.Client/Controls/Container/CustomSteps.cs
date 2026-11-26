using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Sim.Admin.Wpf.Client.Controls.Container;

/// <summary>步骤状态：未开始、进行中、已完成、失败。</summary>
public enum CustomStepStatus
{
    Wait,
    Process,
    Finish,
    Error
}

/// <summary>Naive UI 的 n-steps：等宽步骤条容器，子项为 <see cref="CustomStep"/>。</summary>
public sealed class CustomSteps : UniformGrid
{
    public CustomSteps()
    {
        Rows = 1;
        Columns = 1;
    }

    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        Columns = Math.Max(1, InternalChildren.Count);
    }
}

/// <summary>Naive UI 的 n-step：单个步骤，含状态徽标、连接线、标题与描述。</summary>
public sealed class CustomStep : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(CustomStep), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(CustomStep), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(CustomStepStatus), typeof(CustomStep),
        new PropertyMetadata(CustomStepStatus.Wait, OnBadgeSourceChanged));

    public static readonly DependencyProperty IsLastProperty = DependencyProperty.Register(
        nameof(IsLast), typeof(bool), typeof(CustomStep), new PropertyMetadata(false));

    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(
        nameof(Index), typeof(int), typeof(CustomStep),
        new PropertyMetadata(1, OnBadgeSourceChanged));

    public static readonly DependencyProperty BadgeTextProperty = DependencyProperty.Register(
        nameof(BadgeText), typeof(string), typeof(CustomStep), new PropertyMetadata("1"));

    public CustomStep() => RefreshBadgeText();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public CustomStepStatus Status
    {
        get => (CustomStepStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        set => SetValue(IsLastProperty, value);
    }

    public int Index
    {
        get => (int)GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    /// <summary>徽标内显示的文本：序号或状态符号，由状态推导。</summary>
    public string BadgeText => (string)GetValue(BadgeTextProperty);

    private static void OnBadgeSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomStep)d).RefreshBadgeText();

    private void RefreshBadgeText()
    {
        var text = Status switch
        {
            CustomStepStatus.Finish => "✓",
            CustomStepStatus.Error => "×",
            _ => Index.ToString()
        };
        SetCurrentValue(BadgeTextProperty, text);
    }
}
