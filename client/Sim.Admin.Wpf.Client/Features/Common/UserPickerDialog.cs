using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Controls.Inputs;

namespace Sim.Admin.Wpf.Client.Features.Common;

public sealed record UserOption(Guid Id, string? UserName, string? DisplayName)
{
    public string Label => string.IsNullOrWhiteSpace(DisplayName)
        ? UserName ?? Id.ToString()
        : $"{DisplayName}（{UserName}）";
}

public sealed class UserPickerDialog : Window
{
    private readonly Func<string, Task<List<UserOption>>> _search;
    private readonly CustomInput _searchBox = new() { Height = 38, Margin = new Thickness(0, 0, 8, 0) };
    private readonly ListBox _list = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

    public UserOption? SelectedUser { get; private set; }

    public static UserOption? Pick(Window? owner, string title,
        Func<string, Task<List<UserOption>>> search)
    {
        var dialog = new UserPickerDialog(title, search) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.SelectedUser : null;
    }

    private UserPickerDialog(string title, Func<string, Task<List<UserOption>>> search)
    {
        _search = search;
        Title = title;
        Width = 460;
        Height = 540;
        MinWidth = 360;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "PageBrush");
        SetResourceReference(ForegroundProperty, "InkBrush");

        var searchButton = new CustomButton { Content = "搜索", Width = 78, Height = 34 };
        searchButton.Click += async (_, _) => await SearchAsync();
        var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(searchButton, Dock.Right);
        searchRow.Children.Add(searchButton);
        searchRow.Children.Add(_searchBox);
        _searchBox.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) await SearchAsync();
        };

        _list.DisplayMemberPath = nameof(UserOption.Label);
        _list.Margin = new Thickness(0, 0, 0, 8);
        _list.SetResourceReference(BackgroundProperty, "PaperBrush");
        _list.SetResourceReference(ForegroundProperty, "InkBrush");
        _list.SetResourceReference(BorderBrushProperty, "EdgeBrush");
        _list.MouseDoubleClick += (_, _) => Confirm();
        _status.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");

        var confirm = new CustomButton { Content = "确定", Variant = CustomButtonVariant.Primary, Width = 90 };
        confirm.Click += (_, _) => Confirm();
        var cancel = new CustomButton { Content = "取消", Width = 90, Margin = new Thickness(8, 0, 0, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        actions.Children.Add(confirm);
        actions.Children.Add(cancel);

        var layout = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(searchRow, Dock.Top);
        DockPanel.SetDock(actions, Dock.Bottom);
        layout.Children.Add(searchRow);
        layout.Children.Add(actions);
        var center = new DockPanel();
        DockPanel.SetDock(_status, Dock.Bottom);
        center.Children.Add(_status);
        center.Children.Add(_list);
        layout.Children.Add(center);
        Content = layout;

        Loaded += async (_, _) => await SearchAsync();
    }

    private async Task SearchAsync()
    {
        _status.Text = "正在搜索…";
        try
        {
            var users = await _search(_searchBox.Text.Trim());
            _list.ItemsSource = users;
            _status.Text = users.Count == 0 ? "没有匹配的用户" : $"共 {users.Count} 个结果";
        }
        catch (Exception error)
        {
            _status.Text = $"搜索失败：{error.Message}";
        }
    }

    private void Confirm()
    {
        if (_list.SelectedItem is not UserOption user)
        {
            _status.Text = "请选择一个用户。";
            return;
        }
        SelectedUser = user;
        DialogResult = true;
    }
}
