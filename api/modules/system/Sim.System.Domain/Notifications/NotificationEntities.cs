using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;

namespace Sim.System.Domain.Notifications;

[CodeFirst]
[SugarTable("system_notification_provider")]
[SugarIndex("uidx_system_notification_provider", nameof(TenantId), OrderByType.Asc,
    nameof(Channel), OrderByType.Asc, nameof(Provider), OrderByType.Asc, true)]
public sealed class NotificationProviderConfigurationEntity : AuditedAggregateRoot
{
    public NotificationProviderConfigurationEntity() { }
    public NotificationProviderConfigurationEntity(Guid id) : base(id) { }

    [SugarColumn(Length = 32)] public string Channel { get; set; } = string.Empty;
    [SugarColumn(Length = 64)] public string Provider { get; set; } = string.Empty;
    [SugarColumn(Length = 128)] public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Priority { get; set; } = 100;
    [SugarColumn(ColumnDataType = "text")] public string SettingsJson { get; set; } = "{}";
    [SugarColumn(ColumnDataType = "text", IsNullable = true)] public string? ProtectedSecrets { get; set; }
}

[CodeFirst]
[SugarTable("system_notification_template")]
[SugarIndex("uidx_system_notification_template", nameof(TenantId), OrderByType.Asc,
    nameof(Key), OrderByType.Asc, nameof(Channel), OrderByType.Asc,
    nameof(Locale), OrderByType.Asc, true)]
public sealed class NotificationTemplateEntity : AuditedAggregateRoot
{
    public NotificationTemplateEntity() { }
    public NotificationTemplateEntity(Guid id) : base(id) { }

    [SugarColumn(Length = 128)] public string Key { get; set; } = string.Empty;
    [SugarColumn(Length = 32)] public string Channel { get; set; } = string.Empty;
    [SugarColumn(Length = 16)] public string Locale { get; set; } = "zh-CN";
    [SugarColumn(Length = 512, IsNullable = true)] public string? Subject { get; set; }
    [SugarColumn(ColumnDataType = "text")] public string Body { get; set; } = string.Empty;
    [SugarColumn(Length = 128, IsNullable = true)] public string? ProviderTemplateCode { get; set; }
    public bool Enabled { get; set; }
}

[CodeFirst]
[SugarTable("system_notification_rule")]
[SugarIndex("idx_system_notification_rule_event", nameof(EventName), OrderByType.Asc,
    nameof(Enabled), OrderByType.Asc)]
public sealed class NotificationRuleEntity : AuditedAggregateRoot
{
    public NotificationRuleEntity() { }
    public NotificationRuleEntity(Guid id) : base(id) { }

    [SugarColumn(Length = 256)] public string EventName { get; set; } = string.Empty;
    [SugarColumn(Length = 32)] public string Channel { get; set; } = string.Empty;
    [SugarColumn(Length = 64, IsNullable = true)] public string? Provider { get; set; }
    [SugarColumn(Length = 128)] public string TemplateKey { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

[CodeFirst]
[SugarTable("system_notification_webhook")]
[SugarIndex("uidx_system_notification_webhook", nameof(TenantId), OrderByType.Asc,
    nameof(Site), OrderByType.Asc, nameof(Name), OrderByType.Asc, true)]
public sealed class NotificationWebhookEndpointEntity : AuditedAggregateRoot
{
    public NotificationWebhookEndpointEntity() { }
    public NotificationWebhookEndpointEntity(Guid id) : base(id) { }

    [SugarColumn(Length = 128)] public string Site { get; set; } = string.Empty;
    [SugarColumn(Length = 128)] public string Name { get; set; } = string.Empty;
    [SugarColumn(Length = 2048)] public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    [SugarColumn(ColumnDataType = "text")] public string EventNamesJson { get; set; } = "[]";
    [SugarColumn(ColumnDataType = "text", IsNullable = true)] public string? ProtectedSigningSecret { get; set; }
}

[CodeFirst]
[SugarTable("system_notification_delivery")]
[SugarIndex("uidx_system_notification_delivery_key", nameof(TenantId), OrderByType.Asc,
    nameof(IdempotencyKey), OrderByType.Asc, true)]
[SugarIndex("idx_system_notification_delivery_due", nameof(Status), OrderByType.Asc,
    nameof(NextAttemptAtUtc), OrderByType.Asc)]
public sealed class NotificationDeliveryEntity : AuditedAggregateRoot
{
    public NotificationDeliveryEntity() { }
    public NotificationDeliveryEntity(Guid id) : base(id) { }

    public Guid EventId { get; set; }
    [SugarColumn(Length = 256)] public string EventName { get; set; } = string.Empty;
    [SugarColumn(Length = 32)] public string Channel { get; set; } = string.Empty;
    [SugarColumn(Length = 64)] public string Provider { get; set; } = string.Empty;
    [SugarColumn(Length = 512)] public string DestinationMasked { get; set; } = string.Empty;
    [SugarColumn(ColumnDataType = "text")] public string ProtectedDestination { get; set; } = string.Empty;
    [SugarColumn(Length = 512, IsNullable = true)] public string? Subject { get; set; }
    [SugarColumn(ColumnDataType = "text")] public string Body { get; set; } = string.Empty;
    [SugarColumn(Length = 128, IsNullable = true)] public string? TemplateCode { get; set; }
    [SugarColumn(ColumnDataType = "text")] public string ParametersJson { get; set; } = "{}";
    [SugarColumn(Length = 512)] public string IdempotencyKey { get; set; } = string.Empty;
    [SugarColumn(Length = 32)] public string Status { get; set; } = "created";
    public int Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    [SugarColumn(Length = 256, IsNullable = true)] public string? ProviderRequestId { get; set; }
    [SugarColumn(Length = 128, IsNullable = true)] public string? ErrorCode { get; set; }
    [SugarColumn(Length = 2048, IsNullable = true)] public string? ErrorMessage { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

[CodeFirst]
[SugarTable("system_user_notification")]
[SugarIndex("idx_system_user_notification_unread", nameof(TenantId), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc, nameof(IsRead), OrderByType.Asc,
    nameof(CreationTime), OrderByType.Desc)]
public sealed class UserNotificationEntity : AuditedAggregateRoot
{
    public UserNotificationEntity() { }
    public UserNotificationEntity(Guid id) : base(id) { }

    public Guid UserId { get; set; }
    public Guid EventId { get; set; }
    [SugarColumn(Length = 512)] public string Title { get; set; } = string.Empty;
    [SugarColumn(ColumnDataType = "text")] public string Body { get; set; } = string.Empty;
    [SugarColumn(Length = 2048, IsNullable = true)] public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

[CodeFirst]
[SugarTable("system_browser_push_subscription")]
[SugarIndex("uidx_system_browser_push_endpoint", nameof(TenantId), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc, nameof(EndpointHash), OrderByType.Asc, true)]
public sealed class BrowserPushSubscriptionEntity : AuditedAggregateRoot
{
    public BrowserPushSubscriptionEntity() { }
    public BrowserPushSubscriptionEntity(Guid id) : base(id) { }

    public Guid UserId { get; set; }
    [SugarColumn(Length = 128)] public string EndpointHash { get; set; } = string.Empty;
    [SugarColumn(ColumnDataType = "text")] public string ProtectedSubscription { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? LastSuccessAtUtc { get; set; }
}
