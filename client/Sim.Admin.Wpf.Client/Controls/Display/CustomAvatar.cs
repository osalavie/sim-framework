using System.Windows;

namespace Sim.Admin.Wpf.Client.Controls.Display;

/// <summary>Naive UI 的 n-avatar：圆形或圆角方形头像，内容可为文字或首字。</summary>
public sealed class CustomAvatar : CustomTonedContentControl
{
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomAvatar), new PropertyMetadata(CustomControlSize.Medium));

    public static readonly DependencyProperty RoundProperty = DependencyProperty.Register(
        nameof(Round), typeof(bool), typeof(CustomAvatar), new PropertyMetadata(true));

    public CustomControlSize Size
    {
        get => (CustomControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool Round
    {
        get => (bool)GetValue(RoundProperty);
        set => SetValue(RoundProperty, value);
    }
}
