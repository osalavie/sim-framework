using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Display;

/// <summary>Naive UI 的 n-descriptions：描述列表容器，子项为 <see cref="CustomDescriptionItem"/>。</summary>
public sealed class CustomDescriptions : ItemsControl
{
    public static readonly DependencyProperty BorderedProperty = DependencyProperty.Register(
        nameof(Bordered), typeof(bool), typeof(CustomDescriptions), new PropertyMetadata(true));

    public bool Bordered
    {
        get => (bool)GetValue(BorderedProperty);
        set => SetValue(BorderedProperty, value);
    }

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        var items = Items.OfType<CustomDescriptionItem>().ToList();
        for (var index = 0; index < items.Count; index++)
        {
            items[index].IsFirst = index == 0;
            items[index].IsLast = index == items.Count - 1;
        }
    }
}

/// <summary>Naive UI 的 n-descriptions-item：一行「标签 / 值」。</summary>
public sealed class CustomDescriptionItem : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(CustomDescriptionItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(
        nameof(LabelWidth), typeof(double), typeof(CustomDescriptionItem), new PropertyMetadata(132d));

    public static readonly DependencyProperty IsFirstProperty = DependencyProperty.Register(
        nameof(IsFirst), typeof(bool), typeof(CustomDescriptionItem), new PropertyMetadata(false));

    public static readonly DependencyProperty IsLastProperty = DependencyProperty.Register(
        nameof(IsLast), typeof(bool), typeof(CustomDescriptionItem), new PropertyMetadata(false));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public double LabelWidth
    {
        get => (double)GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }

    public bool IsFirst
    {
        get => (bool)GetValue(IsFirstProperty);
        set => SetValue(IsFirstProperty, value);
    }

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        set => SetValue(IsLastProperty, value);
    }
}
