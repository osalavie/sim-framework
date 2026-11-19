using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Sim.Admin.Wpf.Client.Controls.Display;

/// <summary>Naive UI 的 n-skeleton：内容占位骨架，可带头像与呼吸动画。</summary>
public sealed class CustomSkeleton : Control
{
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(
        nameof(Lines), typeof(int), typeof(CustomSkeleton),
        new PropertyMetadata(3, OnContentChanged));

    public static readonly DependencyProperty ShowAvatarProperty = DependencyProperty.Register(
        nameof(ShowAvatar), typeof(bool), typeof(CustomSkeleton),
        new PropertyMetadata(false, OnContentChanged));

    public static readonly DependencyProperty AnimatedProperty = DependencyProperty.Register(
        nameof(Animated), typeof(bool), typeof(CustomSkeleton), new PropertyMetadata(false));

    private static readonly double[] WidthRatios = [1.0, 0.92, 0.68];

    public int Lines
    {
        get => (int)GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    public bool ShowAvatar
    {
        get => (bool)GetValue(ShowAvatarProperty);
        set => SetValue(ShowAvatarProperty, value);
    }

    public bool Animated
    {
        get => (bool)GetValue(AnimatedProperty);
        set => SetValue(AnimatedProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        BuildLines();
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CustomSkeleton)d).BuildLines();

    private void BuildLines()
    {
        if (GetTemplateChild("PART_Lines") is not StackPanel lines)
        {
            return;
        }

        lines.Children.Clear();
        var count = Math.Max(1, Lines);
        for (var i = 0; i < count; i++)
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, i == count - 1 ? 0 : 10)
            };
            var ratio = WidthRatios[i % WidthRatios.Length];
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ratio, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - ratio, GridUnitType.Star) });

            var bar = new Rectangle
            {
                Height = 12,
                RadiusX = 6,
                RadiusY = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            bar.SetResourceReference(Shape.FillProperty, "SoftBrush");
            row.Children.Add(bar);
            lines.Children.Add(row);
        }
    }
}
