using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Display;

/// <summary>Naive UI 的 n-alert：语义色提示条，可关闭。</summary>
public sealed class CustomAlert : CustomTonedContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(CustomAlert), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowIconProperty = DependencyProperty.Register(
        nameof(ShowIcon), typeof(bool), typeof(CustomAlert), new PropertyMetadata(true));

    public static readonly DependencyProperty ClosableProperty = DependencyProperty.Register(
        nameof(Closable), typeof(bool), typeof(CustomAlert), new PropertyMetadata(false));

    private Border? _root;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowIcon
    {
        get => (bool)GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public bool Closable
    {
        get => (bool)GetValue(ClosableProperty);
        set => SetValue(ClosableProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _root = GetTemplateChild("PART_Root") as Border;
        if (GetTemplateChild("PART_Close") is Button close)
        {
            close.Click -= OnCloseClick;
            close.Click += OnCloseClick;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (_root is not null)
        {
            _root.Visibility = Visibility.Collapsed;
        }
    }
}
