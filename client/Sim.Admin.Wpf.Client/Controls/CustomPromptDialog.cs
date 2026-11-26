using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls;

public sealed record CustomPromptField(
    string Label, bool Secret = false, bool Multiline = false, bool ReadOnly = false,
    string? Value = null, bool Optional = false);

public sealed class CustomPromptDialog : Window
{
    private readonly List<(CustomPromptField Field, TextBox? TextBox, Controls.Inputs.CustomPasswordInput? Password)> _inputs = [];
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };

    public string[] Values { get; private set; } = [];

    public static string[]? Show(Window? owner, string title, string? description,
        IReadOnlyList<CustomPromptField> fields, string confirmText = "确定", bool danger = false)
    {
        var dialog = new CustomPromptDialog(title, description, fields, confirmText, danger) { Owner = owner };
        var ownerHeight = owner is { ActualHeight: > 0 } ? owner.ActualHeight : SystemParameters.WorkArea.Height;
        var availableHeight = Math.Max(300, Math.Min(ownerHeight - 32, SystemParameters.WorkArea.Height - 32));
        dialog.MaxHeight = availableHeight;
        if (fields.Count >= 7)
        {
            dialog.SizeToContent = SizeToContent.Manual;
            dialog.Height = availableHeight;
        }
        return dialog.ShowDialog() == true ? dialog.Values : null;
    }

    private CustomPromptDialog(string title, string? description, IReadOnlyList<CustomPromptField> fields,
        string confirmText, bool danger)
    {
        Title = title;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MaxHeight = Math.Max(400, SystemParameters.WorkArea.Height - 40);
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "PageBrush");
        SetResourceReference(ForegroundProperty, "InkBrush");
        _error.SetResourceReference(ForegroundProperty, "DangerBrush");

        var body = new StackPanel();
        if (!string.IsNullOrWhiteSpace(description))
        {
            var hint = new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 18)
            };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
            body.Children.Add(hint);
        }

        foreach (var field in fields)
        {
            body.Children.Add(new TextBlock
            {
                Text = field.Label,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 6)
            });

            if (field.Secret)
            {
                var password = new Controls.Inputs.CustomPasswordInput
                {
                    Margin = new Thickness(0, 0, 0, 14)
                };
                if (field.Value is not null) password.Password = field.Value;
                body.Children.Add(password);
                _inputs.Add((field, null, password));
            }
            else
            {
                var multiline = field.Multiline;
                TextBox text = multiline
                    ? new Controls.Inputs.CustomTextarea
                    {
                        Text = field.Value ?? string.Empty,
                        Margin = new Thickness(0, 0, 0, 14),
                        IsReadOnly = field.ReadOnly,
                        Height = 160
                    }
                    : new Controls.Inputs.CustomInput
                    {
                        Text = field.Value ?? string.Empty,
                        Margin = new Thickness(0, 0, 0, 14),
                        IsReadOnly = field.ReadOnly
                    };
                body.Children.Add(text);
                _inputs.Add((field, (TextBox)text, null));
            }
        }

        var confirm = new CustomButton
        {
            Content = confirmText,
            MinWidth = 96,
            Variant = danger ? CustomButtonVariant.Danger : CustomButtonVariant.Primary
        };
        confirm.Click += ConfirmClick;
        var cancel = new CustomButton { Content = "取消", MinWidth = 96, Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0)
        };
        actions.Children.Add(confirm);
        actions.Children.Add(cancel);
        var footer = new StackPanel();
        footer.Children.Add(_error);
        footer.Children.Add(actions);
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var fieldsScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = body
        };
        Grid.SetRow(fieldsScroll, 0);
        Grid.SetRow(footer, 1);
        root.Children.Add(fieldsScroll);
        root.Children.Add(footer);
        Content = root;
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        var values = new List<string>();
        foreach (var (field, textBox, passwordBox) in _inputs)
        {
            var value = passwordBox?.Password ?? textBox?.Text ?? string.Empty;
            if (!field.ReadOnly && !field.Optional && value.Length == 0)
            {
                _error.Text = $"请填写{field.Label}。";
                var input = (Control?)passwordBox ?? textBox;
                input?.BringIntoView();
                input?.Focus();
                return;
            }
            values.Add(value);
        }
        Values = values.ToArray();
        DialogResult = true;
    }
}
