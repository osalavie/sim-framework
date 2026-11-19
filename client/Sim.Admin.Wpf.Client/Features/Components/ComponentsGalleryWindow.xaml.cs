using System.Windows;
using System.Windows.Controls;
using Sim.Admin.Wpf.Client.Controls;
using Sim.Admin.Wpf.Client.Controls.Container;
using Sim.Admin.Wpf.Client.Controls.Overlay;

namespace Sim.Admin.Wpf.Client.Features.Components;

/// <summary>组件库预览窗口：集中展示 Naive UI 复刻控件的外观与交互。</summary>
public partial class ComponentsGalleryWindow : Window
{
    public ComponentsGalleryWindow()
    {
        InitializeComponent();
        TableDemo.ItemsSource = new[]
        {
            new TableDemoRow("Sim Admin WPF Client", "PC", "2026-11-11 11:00", true),
            new TableDemoRow("Sim Admin Web", "浏览器", "2026-11-11 10:42", false),
            new TableDemoRow("Sim Admin Mobile", "Android", "2026-11-10 21:18", false)
        };
        MenuDemo.ItemsSource = new[]
        {
            new CustomMenuNode("home", "工作台", "\uE80F"),
            new CustomMenuNode("approval", "审批", "\uE9D5",
                new CustomMenuNode("approval.todo", "我的待办", "\uE8FD"),
                new CustomMenuNode("approval.handled", "我的已办", "\uE930"),
                new CustomMenuNode("approval.monitor", "实例监控", "\uE9D9")),
            new CustomMenuNode("system", "系统管理", "\uE713",
                new CustomMenuNode("system.storage", "文件存储", "\uE8B7"),
                new CustomMenuNode("system.schedule", "定时任务", "\uE823"),
                new CustomMenuNode("system.config", "系统配置", "\uE9F9"),
                new CustomMenuNode("system.organization", "组织架构", "\uE902")),
            new CustomMenuNode("authorization", "授权管理", "\uE192",
                new CustomMenuNode("auth.user", "用户管理", "\uE77B"),
                new CustomMenuNode("auth.role", "角色管理", "\uE716")),
            new CustomMenuNode("profile", "个人资料", "\uE77B")
        };
        MenuDemo.Select("auth.user");
    }

    private sealed record TableDemoRow(string Device, string Kind, string LastSeen, bool Current);

    private void ShowModalClick(object sender, RoutedEventArgs e)
    {
        var content = new TextBlock
        {
            Text = "弹窗正文可以是任意 WPF 元素；确认按钮使用主行动色，取消按钮为次按钮。",
            TextWrapping = TextWrapping.Wrap
        };
        CustomModal.Show(this, "示例弹窗", content, confirmText: "确认", cancelText: "取消");
    }

    private void ShowDrawerClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "抽屉停靠在窗口右侧，内容可滚动。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        content.Children.Add(new CustomButton
        {
            Content = "抽屉内的按钮",
            Variant = CustomButtonVariant.Primary,
            HorizontalAlignment = HorizontalAlignment.Left
        });
        CustomDrawer.Show(this, "示例抽屉", content);
    }

    private void ShowPopconfirmClick(object sender, RoutedEventArgs e)
    {
        var confirmed = CustomPopconfirm.Confirm(this, "确认删除该记录？",
            "删除后不可恢复，请确认操作对象。", "删除", danger: true);
        CustomMessage.Show(this, confirmed ? "已确认操作" : "已取消操作",
            confirmed ? CustomTone.Success : CustomTone.Default);
    }

    private void ShowMessageClick(object sender, RoutedEventArgs e)
    {
        CustomMessage.Success(this, "这是成功消息提示");
        CustomMessage.Warning(this, "这是警告消息提示");
        CustomMessage.Error(this, "这是错误消息提示");
    }
}
