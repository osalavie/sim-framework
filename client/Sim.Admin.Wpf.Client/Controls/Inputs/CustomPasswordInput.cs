using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Inputs;

/// <summary>Naive UI 的 n-input type="password"：密码输入，文本为空时显示占位提示。
/// WPF 的 <see cref="PasswordBox"/> 是密封类型无法继承，因此本控件由 Control 与内部 PasswordBox 组合实现，
/// 对外保持 Placeholder / Size / IsInvalid 契约，并额外提供 Password 依赖属性以便绑定。</summary>
public sealed class CustomPasswordInput : Control
{
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(CustomPasswordInput), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomPasswordInput), new PropertyMetadata(CustomControlSize.Medium));

    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(CustomPasswordInput), new PropertyMetadata(false));

    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
        nameof(Password), typeof(string), typeof(CustomPasswordInput),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPasswordPropertyChanged));

    private PasswordBox? _passwordBox;
    private TextBlock? _placeholder;
    private bool _syncingPassword;

    /// <summary>占位提示，密码为空时显示。</summary>
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

    /// <summary>密码文本，支持双向绑定；与内部 PasswordBox 保持同步。</summary>
    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    /// <summary>清空密码，迁移自 <see cref="PasswordBox.Clear"/> 的调用习惯。</summary>
    public void Clear() => SetCurrentValue(PasswordProperty, string.Empty);

    /// <summary>把焦点移入内部密码框。</summary>
    public new bool Focus()
    {
        if (_passwordBox is null)
        {
            return base.Focus();
        }

        return _passwordBox.Focus() || base.Focus();
    }

    public override void OnApplyTemplate()
    {
        if (_passwordBox is not null)
        {
            _passwordBox.PasswordChanged -= OnInnerPasswordChanged;
        }

        base.OnApplyTemplate();

        _passwordBox = GetTemplateChild("PART_Password") as PasswordBox;
        _placeholder = GetTemplateChild("PlaceholderText") as TextBlock;

        if (_passwordBox is not null)
        {
            _passwordBox.PasswordChanged += OnInnerPasswordChanged;
            _syncingPassword = true;
            _passwordBox.Password = Password;
            _syncingPassword = false;
        }

        UpdatePlaceholder();
    }

    private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (CustomPasswordInput)d;
        if (input._passwordBox is null || input._syncingPassword)
        {
            return;
        }

        input._syncingPassword = true;
        input._passwordBox.Password = (string)e.NewValue;
        input._syncingPassword = false;
        input.UpdatePlaceholder();
    }

    private void OnInnerPasswordChanged(object sender, RoutedEventArgs e)
    {
        UpdatePlaceholder();
        if (_passwordBox is null || _syncingPassword)
        {
            return;
        }

        _syncingPassword = true;
        SetCurrentValue(PasswordProperty, _passwordBox.Password);
        _syncingPassword = false;
    }

    private void UpdatePlaceholder()
    {
        if (_placeholder is null)
        {
            return;
        }

        var length = _passwordBox?.Password.Length ?? Password.Length;
        _placeholder.Visibility = length == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
