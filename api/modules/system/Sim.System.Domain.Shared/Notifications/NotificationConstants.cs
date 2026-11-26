namespace Sim.System.Notifications;

public static class NotificationChannels
{
    public const string Inbox = "inbox";
    public const string Browser = "browser";
    public const string Email = "email";
    public const string Sms = "sms";
    public const string Webhook = "webhook";
    public const string Feishu = "feishu";
    public const string WeCom = "wecom";
    public const string DingTalk = "dingtalk";
    public const string Voice = "voice";
}

public static class NotificationProviders
{
    public const string Inbox = "inbox";
    public const string BrowserWebPush = "browser_web_push";
    public const string Smtp = "smtp";
    public const string TencentSms = "tencent_sms";
    public const string AliyunSms = "aliyun_sms";
    public const string StandardSms = "standard_http_sms";
    public const string GenericWebhook = "generic_webhook";
    public const string Feishu = "feishu_robot";
    public const string WeCom = "wecom_robot";
    public const string DingTalk = "dingtalk_robot";
    public const string TencentVoice = "tencent_voice";
    public const string AliyunVoice = "aliyun_voice";
    public const string StandardVoice = "standard_http_voice";
}

public static class NotificationMessageTypes
{
    public const string Event = "sim.system.notification.event.v1";
}

public static class NotificationDeliveryStatuses
{
    public const string Created = "created";
    public const string Sending = "sending";
    public const string Accepted = "accepted";
    public const string Delivered = "delivered";
    public const string RetryScheduled = "retry_scheduled";
    public const string Failed = "failed";
    public const string Unknown = "unknown";
    public const string Expired = "expired";
}

public static class NotificationPermissions
{
    public const string Manage = "system:notification:manage";
    public const string ViewDeliveries = "system:notification:view-deliveries";
}
