using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls;

public enum CustomButtonVariant
{
    Secondary,
    Primary,
    Danger,
    Ghost,
    Tertiary,
    Text
}

public sealed class CustomButton : Button
{
    public static readonly DependencyProperty VariantProperty = DependencyProperty.Register(
        nameof(Variant), typeof(CustomButtonVariant), typeof(CustomButton),
        new PropertyMetadata(CustomButtonVariant.Secondary));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomButton),
        new PropertyMetadata(CustomControlSize.Medium));

    public static readonly DependencyProperty LoadingProperty = DependencyProperty.Register(
        nameof(Loading), typeof(bool), typeof(CustomButton), new PropertyMetadata(false));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(CustomButton), new PropertyMetadata(null));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(CornerRadius), typeof(CustomButton),
        new PropertyMetadata(new CornerRadius(10)));

    public CustomButtonVariant Variant
    {
        get => (CustomButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public CustomControlSize Size
    {
        get => (CustomControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool Loading
    {
        get => (bool)GetValue(LoadingProperty);
        set => SetValue(LoadingProperty, value);
    }

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
}
