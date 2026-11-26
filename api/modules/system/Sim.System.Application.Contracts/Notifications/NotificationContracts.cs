using System.ComponentModel.DataAnnotations;
using Sim.Framework.Application.Contracts.IServices;

namespace Sim.System.Application.Contracts.Notifications;

public sealed class NotificationEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();

    [Required, MaxLength(256)]
    public string EventName { get; set; } = string.Empty;

    public int Version { get; set; } = 1;
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid? TenantId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CausationId { get; set; }
    public List<NotificationRecipient> Recipients { get; set; } = [];
    public Dictionary<string, string> Data { get; set; } = new(StringComparer.Ordinal);
    public string Priority { get; set; } = "normal";
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public string? IdempotencyKey { get; set; }
}

public sealed class NotificationRecipient
{
    public Guid? UserId { get; set; }
    public string? Address { get; set; }
    public string? Channel { get; set; }
}

public interface INotificationEventPublisher
{
    Task PublishAsync(NotificationEvent notificationEvent, CancellationToken cancellationToken = default);
}

public sealed class SecurityNotificationCommand
{
    [Required, MaxLength(32)]
    public string Channel { get; set; } = string.Empty;

    [Required, MaxLength(512)]
    public string Destination { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Purpose { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string Code { get; set; } = string.Empty;
}

public sealed record SecurityNotificationResult(
    bool Succeeded,
    string Status,
    string? ErrorCode = null);

public sealed record NotificationAccount(
    Guid UserId,
    string Account,
    string? DisplayName,
    string? Email,
    string? Phone);

public interface INotificationAccountDirectory
{
    Task<IReadOnlyList<NotificationAccount>> SearchAsync(
        string? keyword,
        int maxCount,
        CancellationToken cancellationToken = default);

    Task<NotificationAccount?> FindAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IUserNotificationRealtimePublisher
{
    Task PublishChangedAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface ISecurityNotificationSender
{
    Task<SecurityNotificationResult> SendAsync(
        SecurityNotificationCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class NotificationProviderConfigurationDto
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, string> Settings { get; set; } = [];
    public bool HasSecret { get; set; }
}

public sealed class SaveNotificationProviderConfigurationDto
{
    public Guid? Id { get; set; }

    [Required, MaxLength(32)]
    public string Channel { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string Provider { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    [Range(0, 1000)]
    public int Priority { get; set; } = 100;

    public Dictionary<string, string> Settings { get; set; } = [];
    public Dictionary<string, string> Secrets { get; set; } = [];
    public bool ClearSecrets { get; set; }
}

public sealed class NotificationTemplateDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Locale { get; set; } = "zh-CN";
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? ProviderTemplateCode { get; set; }
    public bool Enabled { get; set; }
}

public sealed class SaveNotificationTemplateDto
{
    public Guid? Id { get; set; }

    [Required, MaxLength(128)]
    public string Key { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string Channel { get; set; } = string.Empty;

    [Required, MaxLength(16)]
    public string Locale { get; set; } = "zh-CN";

    [MaxLength(512)]
    public string? Subject { get; set; }

    [Required, MaxLength(20000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? ProviderTemplateCode { get; set; }

    public bool Enabled { get; set; } = true;
}

public sealed class NotificationRuleDto
{
    public Guid Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class SaveNotificationRuleDto
{
    public Guid? Id { get; set; }

    [Required, MaxLength(256)]
    public string EventName { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string Channel { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? Provider { get; set; }

    [Required, MaxLength(128)]
    public string TemplateKey { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}

public sealed class WebhookEndpointDto
{
    public Guid Id { get; set; }
    public string Site { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public List<string> EventNames { get; set; } = [];
    public bool HasSecret { get; set; }
}

public sealed class SaveWebhookEndpointDto
{
    public Guid? Id { get; set; }

    [Required, MaxLength(128)]
    public string Site { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(2048), Url]
    public string Url { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
    public List<string> EventNames { get; set; } = [];

    [MaxLength(2048)]
    public string? SigningSecret { get; set; }

    public bool ClearSigningSecret { get; set; }
}

public sealed class NotificationDeliveryDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string DestinationMasked { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
}

public sealed class NotificationAdministrationSnapshotDto
{
    public List<NotificationProviderConfigurationDto> Providers { get; set; } = [];
    public List<NotificationTemplateDto> Templates { get; set; } = [];
    public List<NotificationRuleDto> Rules { get; set; } = [];
    public List<WebhookEndpointDto> Webhooks { get; set; } = [];
}

public sealed class NotificationAccountDto
{
    public Guid UserId { get; set; }
    public string Account { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string DestinationMasked { get; set; } = string.Empty;
}

public interface INotificationAdministrationService : IApplicationService
{
    Task<NotificationAdministrationSnapshotDto> GetAsync(CancellationToken cancellationToken = default);
    Task<NotificationProviderConfigurationDto> SaveProviderAsync(SaveNotificationProviderConfigurationDto input, CancellationToken cancellationToken = default);
    Task<NotificationTemplateDto> SaveTemplateAsync(SaveNotificationTemplateDto input, CancellationToken cancellationToken = default);
    Task<NotificationRuleDto> SaveRuleAsync(SaveNotificationRuleDto input, CancellationToken cancellationToken = default);
    Task<WebhookEndpointDto> SaveWebhookAsync(SaveWebhookEndpointDto input, CancellationToken cancellationToken = default);
    Task<List<NotificationDeliveryDto>> GetDeliveriesAsync(int maxCount = 100, CancellationToken cancellationToken = default);
    Task<List<NotificationAccountDto>> GetAccountsAsync(
        string channel,
        string? keyword = null,
        int maxCount = 20,
        CancellationToken cancellationToken = default);
    Task<SecurityNotificationResult> TestProviderAsync(
        Guid id,
        string? destination = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}

public sealed class UserNotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreationTime { get; set; }
}

public sealed record UserNotificationSummaryDto(int UnreadCount, List<UserNotificationDto> Items);

public interface IUserNotificationService : IApplicationService
{
    Task<UserNotificationSummaryDto> GetAsync(int maxCount = 20, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}
