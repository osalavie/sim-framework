using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Sim.Admin.Wpf.Client.Controls.Inputs;

/// <summary>Naive UI 的 n-input-number：数值输入，带步进按钮与边界钳制。</summary>
public sealed class CustomInputNumber : Control
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(CustomInputNumber),
        new FrameworkPropertyMetadata(
            0d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValuePropertyChanged,
            CoerceCurrentValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(CustomInputNumber),
        new PropertyMetadata(0d, OnBoundsPropertyChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(CustomInputNumber),
        new PropertyMetadata(100d, OnBoundsPropertyChanged));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(CustomInputNumber), new PropertyMetadata(1d));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(CustomControlSize), typeof(CustomInputNumber), new PropertyMetadata(CustomControlSize.Medium));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(CustomInputNumber), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(
        nameof(IsInvalid), typeof(bool), typeof(CustomInputNumber), new PropertyMetadata(false));

    private TextBox? _editor;
    private ButtonBase? _decrease;
    private ButtonBase? _increase;
    private TextBlock? _placeholder;

    /// <summary>数值变化事件，供 Gallery 与业务页监听。</summary>
    public event EventHandler? ValueChanged;

    /// <summary>当前数值，自动钳制在 Minimum 与 Maximum 之间。</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>允许的最小值，默认 0。</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>允许的最大值，默认 100。</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>每次步进增减的数值，默认 1。</summary>
    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>控件尺寸，默认 Medium（36 像素高）。</summary>
    public CustomControlSize Size
    {
        get => (CustomControlSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>占位提示，编辑框为空时显示。</summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>校验失败态，描边切换为 DangerBrush。</summary>
    public bool IsInvalid
    {
        get => (bool)GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    public override void OnApplyTemplate()
    {
        DetachTemplateParts();
        base.OnApplyTemplate();

        _editor = GetTemplateChild("PART_Editor") as TextBox;
        _decrease = GetTemplateChild("PART_Decrease") as ButtonBase;
        _increase = GetTemplateChild("PART_Increase") as ButtonBase;
        _placeholder = GetTemplateChild("PlaceholderText") as TextBlock;

        if (_editor is not null)
        {
            _editor.LostFocus += OnEditorLostFocus;
            _editor.KeyDown += OnEditorKeyDown;
            _editor.TextChanged += OnEditorTextChanged;
        }

        if (_decrease is not null)
        {
            _decrease.Click += OnDecreaseClick;
        }

        if (_increase is not null)
        {
            _increase.Click += OnIncreaseClick;
        }

        RefreshEditorText();
        RefreshPlaceholder();
        RefreshButtons();
    }

    private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (CustomInputNumber)d;
        input.RefreshEditorText();
        input.RefreshButtons();
        input.ValueChanged?.Invoke(input, EventArgs.Empty);
    }

    private static void OnBoundsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var input = (CustomInputNumber)d;
        input.CoerceValue(ValueProperty);
        input.RefreshButtons();
    }

    private static object CoerceCurrentValue(DependencyObject d, object baseValue)
    {
        var input = (CustomInputNumber)d;
        var value = (double)baseValue;
        return double.IsNaN(value) ? input.Clamp(input.Minimum) : input.Clamp(value);
    }

    private void DetachTemplateParts()
    {
        if (_editor is not null)
        {
            _editor.LostFocus -= OnEditorLostFocus;
            _editor.KeyDown -= OnEditorKeyDown;
            _editor.TextChanged -= OnEditorTextChanged;
        }

        if (_decrease is not null)
        {
            _decrease.Click -= OnDecreaseClick;
        }

        if (_increase is not null)
        {
            _increase.Click -= OnIncreaseClick;
        }

        _editor = null;
        _decrease = null;
        _increase = null;
        _placeholder = null;
    }

    private double Clamp(double value)
    {
        var low = Math.Min(Minimum, Maximum);
        var high = Math.Max(Minimum, Maximum);
        return Math.Clamp(value, low, high);
    }

    private void RefreshEditorText()
    {
        if (_editor is null)
        {
            return;
        }

        var text = Value.ToString("0.##", CultureInfo.InvariantCulture);
        if (!string.Equals(_editor.Text, text, StringComparison.Ordinal))
        {
            _editor.Text = text;
        }
    }

    private void RefreshPlaceholder()
    {
        if (_placeholder is null)
        {
            return;
        }

        _placeholder.Visibility = _editor is null || _editor.Text.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void RefreshButtons()
    {
        if (_decrease is not null)
        {
            _decrease.IsEnabled = Value > Math.Min(Minimum, Maximum);
        }

        if (_increase is not null)
        {
            _increase.IsEnabled = Value < Math.Max(Minimum, Maximum);
        }
    }

    private void CommitEditorText()
    {
        if (_editor is null)
        {
            return;
        }

        if (double.TryParse(
                _editor.Text,
                NumberStyles.Float | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            SetCurrentValue(ValueProperty, Clamp(parsed));
        }
        else
        {
            RefreshEditorText();
        }
    }

    private void OnEditorLostFocus(object sender, RoutedEventArgs e) => CommitEditorText();

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CommitEditorText();
        e.Handled = true;
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e) => RefreshPlaceholder();

    private void OnDecreaseClick(object sender, RoutedEventArgs e) =>
        SetCurrentValue(ValueProperty, Clamp(Value - Step));

    private void OnIncreaseClick(object sender, RoutedEventArgs e) =>
        SetCurrentValue(ValueProperty, Clamp(Value + Step));
}
