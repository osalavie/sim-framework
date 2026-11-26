using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Overlay;

/// <summary>二次确认浮层，对应 Naive UI 的 n-popconfirm；确认返回 true。</summary>
public static class CustomPopconfirm
{
    /// <summary>显示确认浮层。</summary>
    public static bool Confirm(Window? owner, string title, string description,
        string confirmText = "确定", bool danger = false)
    {
        var window = CustomOverlayBuilder.CreateWindow<Window>(owner, title, 380);
        window.SizeToContent = SizeToContent.Height;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var body = new StackPanel { Margin = new Thickness(22) };
        body.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        var hint = new TextBlock
        {
            Text = description,
            Margin = new Thickness(0, 8, 0, 0),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        body.Children.Add(hint);

        var confirm = CustomOverlayBuilder.CreateButton(confirmText,
            danger ? CustomButtonVariant.Danger : CustomButtonVariant.Primary);
        var cancel = CustomOverlayBuilder.CreateButton("取消", CustomButtonVariant.Secondary);
        cancel.IsCancel = true;
        confirm.IsDefault = true;
        confirm.Click += (_, _) => window.DialogResult = true;
        cancel.Click += (_, _) => window.DialogResult = false;
        body.Children.Add(CustomOverlayBuilder.CreateActions(confirm, cancel));

        window.Content = body;
        return window.ShowDialog() == true;
    }
}
