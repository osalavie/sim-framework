using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Controls.Inputs;

namespace Sim.Admin.Wpf.Client.Controls;

public partial class CustomFormField : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(CustomFormField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(string), typeof(CustomFormField),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(
        nameof(MaxLength), typeof(int), typeof(CustomFormField), new PropertyMetadata(1000));

    public static readonly DependencyProperty ValidationErrorProperty = DependencyProperty.Register(
        nameof(ValidationError), typeof(string), typeof(CustomFormField),
        new PropertyMetadata(string.Empty, (d, _) => ((CustomFormField)d).UpdateHasError()));

    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(
        nameof(HasError), typeof(bool), typeof(CustomFormField), new PropertyMetadata(false));

    public static readonly DependencyProperty NumericOnlyProperty = DependencyProperty.Register(
        nameof(NumericOnly), typeof(bool), typeof(CustomFormField), new PropertyMetadata(false));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>校验错误文案；为空时隐藏错误行。</summary>
    public string ValidationError
    {
        get => (string)GetValue(ValidationErrorProperty);
        set => SetValue(ValidationErrorProperty, value);
    }

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        private set => SetValue(HasErrorProperty, value);
    }

    /// <summary>只允许输入数字（用于手机号等字段）。</summary>
    public bool NumericOnly
    {
        get => (bool)GetValue(NumericOnlyProperty);
        set => SetValue(NumericOnlyProperty, value);
    }

    public CustomInput Editor => Input;

    public CustomFormField()
    {
        InitializeComponent();
        Input.PreviewTextInput += (_, e) =>
        {
            if (NumericOnly && !e.Text.All(char.IsDigit)) e.Handled = true;
        };
        Input.TextChanged += (_, _) =>
        {
            if (!NumericOnly || Input.Text.All(char.IsDigit)) return;
            var caret = Input.CaretIndex;
            var digits = new string(Input.Text.Where(char.IsDigit).ToArray());
            Input.Text = digits;
            Input.CaretIndex = Math.Min(caret, digits.Length);
        };
        DataObject.AddPastingHandler(Input, OnPaste);
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!NumericOnly) return;
        var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        e.CancelCommand();
        if (digits.Length > 0) Input.SelectedText = digits;
    }

    private void UpdateHasError() => HasError = !string.IsNullOrEmpty(ValidationError);
}
