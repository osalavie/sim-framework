using Sim.System.Notifications;

namespace Sim.System.Application.Notifications;

internal sealed record NotificationProviderDefinition(
    string Channel,
    string Provider,
    string DefaultDisplayName,
    int Priority);

/// <summary>
/// 通知服务商的系统默认清单。显示名称仅作为数据字典的初始种子，
/// 运行时优先使用 <c>system.notification-provider</c> 字典中的管理员配置。
/// </summary>
internal static class NotificationProviderDefinitions
{
    public static readonly IReadOnlyList<NotificationProviderDefinition> All =
    [
        new(NotificationChannels.Inbox, NotificationProviders.Inbox, "站内通知", 0),
        new(NotificationChannels.Browser, NotificationProviders.BrowserWebPush, "浏览器通知", 10),
        new(NotificationChannels.Email, NotificationProviders.Smtp, "SMTP 邮件", 100),
        new(NotificationChannels.Sms, NotificationProviders.TencentSms, "腾讯云短信", 100),
        new(NotificationChannels.Sms, NotificationProviders.AliyunSms, "阿里云短信", 110),
        new(NotificationChannels.Sms, NotificationProviders.StandardSms, "标准 HTTP 短信", 200),
        new(NotificationChannels.Webhook, NotificationProviders.GenericWebhook, "通用 Webhook", 100),
        new(NotificationChannels.Feishu, NotificationProviders.Feishu, "飞书机器人", 100),
        new(NotificationChannels.WeCom, NotificationProviders.WeCom, "企业微信机器人", 100),
        new(NotificationChannels.DingTalk, NotificationProviders.DingTalk, "钉钉机器人", 100),
        new(NotificationChannels.Voice, NotificationProviders.TencentVoice, "腾讯云语音", 100),
        new(NotificationChannels.Voice, NotificationProviders.AliyunVoice, "阿里云语音", 110),
        new(NotificationChannels.Voice, NotificationProviders.StandardVoice, "标准 HTTP 语音", 200)
    ];
}
