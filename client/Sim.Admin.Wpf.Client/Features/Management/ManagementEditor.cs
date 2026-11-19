using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Controls.Inputs;
using Sim.Admin.Wpf.Client.Features.Common;

namespace Sim.Admin.Wpf.Client.Features.Management;

public sealed class ManagementEditor : Window
{
    private readonly ManagementDefinition _definition;
    private readonly List<(ManagementField Field, FrameworkElement Control)> _editors = [];
    private readonly TextBlock _error = new() { Margin = new Thickness(0, 8, 0, 0) };

    public JsonObject? Result { get; private set; }

    public ManagementEditor(ManagementDefinition definition, JsonElement? source = null)
    {
        _definition = definition;
        Title = source is null ? $"新建{definition.Title}" : $"编辑{definition.Title}";
        Width = 520;
        Height = 650;
        MinWidth = 420;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "PageBrush");
        SetResourceReference(ForegroundProperty, "InkBrush");

        var fields = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
        foreach (var field in definition.Fields)
        {
            if (source is not null && field.CreateOnly) continue;
            var value = source is null ? field.DefaultValue ?? string.Empty : ReadValue(source, field);
            if (field.Kind == FieldKind.Boolean)
            {
                var checkbox = new CustomCheckBox
                {
                    Content = field.Label,
                    IsChecked = source is null ? field.DefaultValue != "false" : value == "true",
                    Margin = new Thickness(0, 0, 0, 14)
                };
                fields.Children.Add(checkbox);
                _editors.Add((field, checkbox));
            }
            else if (field.Kind == FieldKind.Choice)
            {
                var options = field.Options ?? throw new InvalidOperationException("选项字段未配置候选值。");
                var selected = int.TryParse(value, out var parsed) ? parsed : options[0].Value;
                var selector = new CustomSelect
                {
                    ItemsSource = options,
                    ItemTemplate = CustomSelect.CreateDisplayTemplate(nameof(FieldOption.Label)),
                    SelectedValuePath = nameof(FieldOption.Value),
                    SelectedValue = selected,
                    Height = 40,
                    Margin = new Thickness(0, 0, 0, 14)
                };
                fields.Children.Add(new TextBlock { Text = field.Label, FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 6) });
                fields.Children.Add(selector);
                _editors.Add((field, selector));
            }
            else if (field.Kind is FieldKind.LongText or FieldKind.Json or FieldKind.List)
            {
                fields.Children.Add(new TextBlock
                {
                    Text = field.Required ? $"{field.Label} *" : field.Label,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 6)
                });
                var text = new CustomTextarea
                {
                    Text = value,
                    Height = 100,
                    Margin = new Thickness(0, 0, 0, 14)
                };
                fields.Children.Add(text);
                _editors.Add((field, text));
            }
            else
            {
                var editor = new CustomFormField
                {
                    Label = field.Required ? $"{field.Label} *" : field.Label,
                    Value = value,
                    MaxLength = field.Key == "phone" ? 11
                        : field.Kind == FieldKind.Number ? 12
                        : field.Key is "userName" or "nickName" ? 20 : 1000,
                    NumericOnly = field.Key == "phone"
                };
                editor.Editor.LostFocus += (_, _) => ValidateEditor(field, editor);
                editor.Editor.TextChanged += (_, _) =>
                {
                    if (editor.HasError) ValidateEditor(field, editor);
                };
                fields.Children.Add(editor);
                _editors.Add((field, editor));
            }
        }

        var save = new CustomButton { Content = "保存", Variant = CustomButtonVariant.Primary, MinWidth = 90 };
        save.Click += SaveClick;
        var cancel = new CustomButton { Content = "取消", MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(save);
        actions.Children.Add(cancel);

        // 右侧留 0，滚动条贴窗口边缘；表单内容与页脚各自保留 24px 呼吸空间。
        var layout = new DockPanel { Margin = new Thickness(24, 24, 0, 24) };
        var footer = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
        _error.SetResourceReference(ForegroundProperty, "DangerBrush");
        footer.Children.Add(actions);
        footer.Children.Add(_error);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);
        layout.Children.Add(new ScrollViewer
        {
            Content = fields,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        Content = layout;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        foreach (var (field, control) in _editors)
        {
            if (control is not CustomFormField form) continue;
            ValidateEditor(field, form);
            if (!form.HasError) continue;
            _error.Text = form.ValidationError;
            return;
        }

        var result = new JsonObject();
        foreach (var (field, control) in _editors)
        {
            if (control is CheckBox checkbox)
            {
                result[field.Key] = checkbox.IsChecked == true;
                continue;
            }
            if (control is ComboBox selector)
            {
                result[field.Key] = (int)(selector.SelectedValue ?? 0);
                continue;
            }
            if (control is TextBox textBox)
            {
                var textValue = textBox.Text.Trim();
                if (field.Required && textValue.Length == 0)
                {
                    _error.Text = $"请填写{field.Label}。";
                    return;
                }
                if (field.Kind == FieldKind.Json)
                {
                    if (textValue.Length == 0) result[field.Key] = null;
                    else
                    {
                        try { result[field.Key] = JsonNode.Parse(textValue); }
                        catch (JsonException)
                        {
                            _error.Text = $"{field.Label}须为有效 JSON。";
                            return;
                        }
                    }
                }
                else if (field.Kind == FieldKind.List)
                {
                    var items = textValue.Split(new[] { '\n', ',' },
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (items.Length == 0) result[field.Key] = null;
                    else
                    {
                        var array = new JsonArray();
                        foreach (var item in items) array.Add(JsonValue.Create(item));
                        result[field.Key] = array;
                    }
                }
                else result[field.Key] = textValue.Length == 0 ? null : textValue;
                continue;
            }

            var value = ((CustomFormField)control).Value.Trim();
            if (field.Required && value.Length == 0)
            {
                _error.Text = $"请填写{field.Label}。";
                return;
            }
            if (field.Kind == FieldKind.Number)
            {
                if (value.Length == 0) continue;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    _error.Text = $"{field.Label}须为整数。";
                    return;
                }
                result[field.Key] = number;
            }
            else if (value.Length > 0 || field.Required) result[field.Key] = value;
            else result[field.Key] = null;
        }
        if (_definition.FixedValues is not null)
            foreach (var (key, value) in _definition.FixedValues)
                result[key] = JsonSerializer.SerializeToNode(value);
        Result = result;
        DialogResult = true;
    }

    private static void ValidateEditor(ManagementField field, CustomFormField editor) =>
        editor.ValidationError = ValidateValue(field, editor.Value) ?? string.Empty;

    private static string? ValidateValue(ManagementField field, string raw)
    {
        var value = raw.Trim();
        if (field.Required && value.Length == 0) return $"请填写{field.Label}。";
        if (value.Length == 0) return null;
        if (field.Key.Equals("userName", StringComparison.OrdinalIgnoreCase)) return FieldValidation.UserName(value);
        if (field.Key.Equals("nickName", StringComparison.OrdinalIgnoreCase)) return FieldValidation.NickName(value);
        if (field.Key.Equals("email", StringComparison.OrdinalIgnoreCase)) return FieldValidation.Email(value);
        if (field.Key.Equals("phone", StringComparison.OrdinalIgnoreCase)) return FieldValidation.Phone(value);
        return null;
    }

    private static string ReadValue(JsonElement? source, ManagementField field)
    {
        if (source is not { ValueKind: JsonValueKind.Object } value ||
            !value.TryGetProperty(field.Key, out var property) || property.ValueKind == JsonValueKind.Null)
            return string.Empty;
        if (field.Kind == FieldKind.List && property.ValueKind == JsonValueKind.Array)
            return string.Join(Environment.NewLine, property.EnumerateArray().Select(item => item.ToString()));
        return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : property.ToString();
    }
}
