namespace Sim.System.Notifications;

public enum NotificationChannelResultStatus
{
    Accepted,
    Delivered,
    TransientFailure,
    PermanentFailure,
    Unknown
}

public sealed record ProviderCapabilities(
    bool SupportsTemplates = false,
    bool SupportsCallbacks = false,
    bool SupportsBatch = false);

public sealed record NotificationChannelRequest(
    Guid DeliveryId,
    string Channel,
    string Provider,
    string Destination,
    string? Subject,
    string Body,
    string? TemplateCode,
    IReadOnlyDictionary<string, string> TemplateParameters,
    IReadOnlyDictionary<string, string> Configuration,
    string IdempotencyKey,
    Guid? TenantId = null);

public sealed record NotificationChannelResult(
    NotificationChannelResultStatus Status,
    string? ProviderRequestId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    bool ConfirmedNotAccepted = false)
{
    public static NotificationChannelResult Delivered(string? requestId = null) =>
        new(NotificationChannelResultStatus.Delivered, requestId);

    public static NotificationChannelResult Accepted(string? requestId = null) =>
        new(NotificationChannelResultStatus.Accepted, requestId);
}

public interface INotificationChannelProvider
{
    string Channel { get; }
    string Provider { get; }
    ProviderCapabilities Capabilities { get; }

    Task<NotificationChannelResult> SendAsync(
        NotificationChannelRequest request,
        CancellationToken cancellationToken = default);
}
