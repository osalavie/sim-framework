using System.Windows;
using System.Windows.Controls;

namespace Sim.Admin.Wpf.Client.Controls.Overlay;

/// <summary>通用弹窗，对应 Naive UI 的 n-modal 对话框形态。</summary>
public static class CustomModal
{
    /// <summary>显示模态对话框，确认返回 true。</summary>
    public static bool Show(Window? owner, string title, UIElement content,
        string confirmText = "确定", string cancelText = "取消", bool danger = false, double width = 520)
    {
        var window = CustomOverlayBuilder.CreateWindow<Window>(owner, title, width);
        window.SizeToContent = SizeToContent.Height;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(CustomOverlayBuilder.CreateTitle(title));
        content.SetCurrentValue(FrameworkElement.MarginProperty, new Thickness(0, 14, 0, 0));
        body.Children.Add(content);

        var confirm = CustomOverlayBuilder.CreateButton(confirmText,
            danger ? CustomButtonVariant.Danger : CustomButtonVariant.Primary);
        var cancel = CustomOverlayBuilder.CreateButton(cancelText, CustomButtonVariant.Secondary);
        cancel.IsCancel = true;
        confirm.IsDefault = true;
        confirm.Click += (_, _) => window.DialogResult = true;
        cancel.Click += (_, _) => window.DialogResult = false;
        body.Children.Add(CustomOverlayBuilder.CreateActions(confirm, cancel));

        window.Content = body;
        return window.ShowDialog() == true;
    }
}
