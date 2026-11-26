using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Controls.Inputs;
using Sim.Admin.Wpf.Client.Services;

namespace Sim.Admin.Wpf.Client.Features.SystemManagement;

public sealed record ScheduleTaskEditResult(string CronExpression, bool Enabled);

public sealed class ScheduleTaskDialog : Window
{
    private readonly ApiClient? _api;
    private readonly ScheduleTask? _task;
    private readonly TextBox _displayName = Field();
    private readonly TextBox _description = Multiline(64);
    private readonly CustomSelect _type = new() { Height = 38, Margin = new Thickness(0, 0, 0, 14) };
    private readonly TextBox _cron = Field("0 0/5 * * * ?");
    private readonly CheckBox _enabled = new() { Content = "启用任务", IsChecked = true, Margin = new Thickness(0, 0, 0, 14) };
    private readonly StackPanel _httpPanel = new();
    private readonly StackPanel _commandPanel = new();
    private readonly StackPanel _servicePanel = new();
    private readonly CustomSelect _httpMethod = new() { Height = 38, Margin = new Thickness(0, 0, 0, 14) };
    private readonly TextBox _url = Field();
    private readonly TextBox _contentType = Field("application/json");
    private readonly TextBox _body = Multiline(80);
    private readonly TextBox _headers = Multiline(80);
    private readonly TextBox _command = Field();
    private readonly TextBox _arguments = Multiline(80);
    private readonly TextBox _workingDirectory = Field();
    private readonly TextBox _timeout = Field("120");
    private readonly CustomSelect _serviceKey = new() { Height = 38, Margin = new Thickness(0, 0, 0, 14) };
    private readonly TextBox _serviceArgs = Multiline(100);
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    public ScheduleTaskCreate? Created { get; private set; }
    public ScheduleTaskEditResult? Edited { get; private set; }

    public static ScheduleTaskCreate? Create(Window? owner, ApiClient api)
    {
        var dialog = new ScheduleTaskDialog(api, null) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Created : null;
    }

    public static ScheduleTaskEditResult? Edit(Window? owner, ScheduleTask task)
    {
        var dialog = new ScheduleTaskDialog(null, task) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Edited : null;
    }

    private ScheduleTaskDialog(ApiClient? api, ScheduleTask? task)
    {
        _api = api;
        _task = task;
        Title = task is null ? "新建定时任务" : "编辑定时任务";
        Width = 640;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "PageBrush");
        SetResourceReference(ForegroundProperty, "InkBrush");
        _error.SetResourceReference(TextBlock.ForegroundProperty, "DangerBrush");
        _enabled.SetResourceReference(CheckBox.ForegroundProperty, "InkBrush");

        var body = new StackPanel { Margin = new Thickness(24) };
        if (task is null) BuildCreate(body);
        else BuildEdit(body, task);

        var confirm = new CustomButton
        {
            Content = task is null ? "创建任务" : "保存",
            Variant = CustomButtonVariant.Primary,
            MinWidth = 100
        };
        confirm.Click += (_, _) =>
        {
            if (task is null) ConfirmCreate();
            else ConfirmEdit(task);
        };
        var cancel = new CustomButton { Content = "取消", MinWidth = 90, Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        actions.Children.Add(confirm);
        actions.Children.Add(cancel);
        body.Children.Add(actions);
        body.Children.Add(_error);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 760,
            Content = body
        };

        if (task is null)
        {
            _type.ItemsSource = new[] { "Http", "Command", "ServiceMethod" };
            _type.SelectedIndex = 0;
            _type.SelectionChanged += (_, _) => ApplyTypeState();
            _httpMethod.ItemsSource = new[] { "GET", "POST", "PUT", "DELETE" };
            _httpMethod.SelectedIndex = 0;
            ApplyTypeState();
            Loaded += async (_, _) => await LoadServiceMethodsAsync();
        }
    }

    private void BuildCreate(StackPanel body)
    {
        body.Children.Add(Label("任务名称 *"));
        body.Children.Add(_displayName);
        body.Children.Add(Label("任务说明"));
        body.Children.Add(_description);
        body.Children.Add(Label("任务类型"));
        body.Children.Add(_type);
        body.Children.Add(Label("Cron 表达式 *"));
        body.Children.Add(_cron);
        body.Children.Add(_enabled);
        body.Children.Add(_httpPanel);
        body.Children.Add(_commandPanel);
        body.Children.Add(_servicePanel);

        _httpPanel.Children.Add(Label("请求方法"));
        _httpPanel.Children.Add(_httpMethod);
        _httpPanel.Children.Add(Label("请求地址 *"));
        _httpPanel.Children.Add(_url);
        _httpPanel.Children.Add(Label("内容类型"));
        _httpPanel.Children.Add(_contentType);
        _httpPanel.Children.Add(Label("请求头（每行一个 Key: Value）"));
        _httpPanel.Children.Add(_headers);
        _httpPanel.Children.Add(Label("请求体"));
        _httpPanel.Children.Add(_body);

        _commandPanel.Children.Add(Label("命令 *"));
        _commandPanel.Children.Add(_command);
        _commandPanel.Children.Add(Label("参数（每行一个）"));
        _commandPanel.Children.Add(_arguments);
        _commandPanel.Children.Add(Label("工作目录"));
        _commandPanel.Children.Add(_workingDirectory);
        _commandPanel.Children.Add(Label("超时秒数"));
        _commandPanel.Children.Add(_timeout);

        _servicePanel.Children.Add(Label("服务方法 *"));
        _servicePanel.Children.Add(_serviceKey);
        _servicePanel.Children.Add(Label("参数 JSON"));
        _servicePanel.Children.Add(_serviceArgs);
    }

    private void BuildEdit(StackPanel body, ScheduleTask task)
    {
        body.Children.Add(new TextBlock
        {
            Text = task.DisplayName ?? task.Key ?? "定时任务",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        var meta = new TextBlock
        {
            Text = $"{task.TaskType}  ·  {task.Group}/{task.Key}",
            Margin = new Thickness(0, 6, 0, 16)
        };
        meta.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        body.Children.Add(meta);
        body.Children.Add(Label("Cron 表达式 *"));
        _cron.Text = task.CronExpression ?? string.Empty;
        body.Children.Add(_cron);
        _enabled.IsChecked = task.Enabled;
        body.Children.Add(_enabled);
    }

    private async Task LoadServiceMethodsAsync()
    {
        if (_api is null) return;
        try
        {
            var methods = await _api.GetScheduleServiceMethodsAsync();
            _serviceKey.ItemsSource = methods;
            _serviceKey.ItemTemplate = CustomSelect.CreateDisplayTemplate(nameof(ScheduleServiceMethod.Label));
            if (methods.Count > 0) _serviceKey.SelectedIndex = 0;
        }
        catch (Exception error)
        {
            _error.Text = $"服务方法加载失败：{error.Message}";
        }
    }

    private void ApplyTypeState()
    {
        var type = _type.SelectedItem as string ?? "Http";
        _httpPanel.Visibility = type == "Http" ? Visibility.Visible : Visibility.Collapsed;
        _commandPanel.Visibility = type == "Command" ? Visibility.Visible : Visibility.Collapsed;
        _servicePanel.Visibility = type == "ServiceMethod" ? Visibility.Visible : Visibility.Collapsed;
        _error.Text = string.Empty;
    }

    private void ConfirmEdit(ScheduleTask task)
    {
        if (string.IsNullOrWhiteSpace(_cron.Text))
        {
            _error.Text = "请填写 Cron 表达式。";
            return;
        }
        Edited = new ScheduleTaskEditResult(_cron.Text.Trim(), _enabled.IsChecked == true);
        DialogResult = true;
    }

    private void ConfirmCreate()
    {
        var displayName = _displayName.Text.Trim();
        var cron = _cron.Text.Trim();
        if (displayName.Length == 0)
        {
            _error.Text = "请填写任务名称。";
            return;
        }
        if (cron.Length == 0)
        {
            _error.Text = "请填写 Cron 表达式。";
            return;
        }
        var type = _type.SelectedItem as string ?? "Http";
        var result = new ScheduleTaskCreate
        {
            DisplayName = displayName,
            Description = string.IsNullOrWhiteSpace(_description.Text) ? null : _description.Text.Trim(),
            TaskType = type,
            CronExpression = cron,
            Enabled = _enabled.IsChecked == true
        };
        if (type == "Http")
        {
            if (string.IsNullOrWhiteSpace(_url.Text))
            {
                _error.Text = "请填写请求地址。";
                return;
            }
            if (!TryParseHeaders(_headers.Text, out var headers, out var headerError))
            {
                _error.Text = headerError;
                return;
            }
            result.HttpMethod = _httpMethod.SelectedItem as string ?? "GET";
            result.Url = _url.Text.Trim();
            result.HttpContentType = string.IsNullOrWhiteSpace(_contentType.Text)
                ? "application/json"
                : _contentType.Text.Trim();
            result.HttpBody = string.IsNullOrWhiteSpace(_body.Text) ? null : _body.Text;
            result.HttpHeaders = headers;
        }
        else if (type == "Command")
        {
            if (string.IsNullOrWhiteSpace(_command.Text))
            {
                _error.Text = "请填写命令。";
                return;
            }
            result.Command = _command.Text.Trim();
            result.CommandArguments = SplitLines(_arguments.Text);
            result.WorkingDirectory = string.IsNullOrWhiteSpace(_workingDirectory.Text)
                ? null
                : _workingDirectory.Text.Trim();
            if (!string.IsNullOrWhiteSpace(_timeout.Text))
            {
                if (!int.TryParse(_timeout.Text.Trim(), out var timeout) || timeout is < 1 or > 3600)
                {
                    _error.Text = "超时秒数须为 1–3600 之间的整数。";
                    return;
                }
                result.TimeoutSeconds = timeout;
            }
        }
        else
        {
            if (_serviceKey.SelectedItem is not ScheduleServiceMethod method)
            {
                _error.Text = "请选择服务方法。";
                return;
            }
            result.ServiceMethodKey = method.Key;
            result.ServiceArgumentsJson = string.IsNullOrWhiteSpace(_serviceArgs.Text)
                ? "{}"
                : _serviceArgs.Text;
        }
        Created = result;
        DialogResult = true;
    }

    private static bool TryParseHeaders(string text, out Dictionary<string, string>? headers,
        out string error)
    {
        headers = null;
        error = string.Empty;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var index = line.IndexOf(':');
            if (index <= 0)
            {
                error = $"请求头格式无效：{line}";
                return false;
            }
            var name = line[..index].Trim();
            var value = line[(index + 1)..].Trim();
            if (name.Length == 0)
            {
                error = $"请求头名称不能为空：{line}";
                return false;
            }
            result[name] = value;
        }
        headers = result.Count == 0 ? null : result;
        return true;
    }

    private static string[]? SplitLines(string text)
    {
        var values = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
        return values.Length == 0 ? null : values;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 0, 0, 6)
    };

    private static TextBox Field(string value = "") => new CustomInput
    {
        Text = value,
        Height = 38,
        Margin = new Thickness(0, 0, 0, 14)
    };

    private static TextBox Multiline(double height) => new CustomTextarea
    {
        Height = height,
        Margin = new Thickness(0, 0, 0, 14)
    };
}
