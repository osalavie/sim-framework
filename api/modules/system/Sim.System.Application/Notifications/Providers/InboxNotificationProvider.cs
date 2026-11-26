using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Notifications;
using Sim.System.Domain.Notifications;
using Sim.System.Notifications;

namespace Sim.System.Application.Notifications.Providers;

public sealed class InboxNotificationProvider(
    ISqlSugarRepository<UserNotificationEntity> repository,
    UserNotificationRealtimeNotifier realtimeNotifier) : INotificationChannelProvider
{
    public string Channel => NotificationChannels.Inbox;
    public string Provider => NotificationProviders.Inbox;
    public ProviderCapabilities Capabilities => new();

    public async Task<NotificationChannelResult> SendAsync(
        NotificationChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(request.Destination, out var userId))
        {
            return new NotificationChannelResult(
                NotificationChannelResultStatus.PermanentFailure,
                ErrorCode: "invalid_user",
                ErrorMessage: "Inbox destination must be a user identifier.",
                ConfirmedNotAccepted: true);
        }

        var link = request.TemplateParameters.GetValueOrDefault("link");
        await repository.InsertAsync(new UserNotificationEntity(Guid.NewGuid())
        {
            TenantId = request.TenantId,
            UserId = userId,
            EventId = request.DeliveryId,
            Title = request.Subject ?? string.Empty,
            Body = request.Body,
            Link = link,
            CreationTime = DateTime.UtcNow
        });
        await realtimeNotifier.PublishChangedAsync(userId, cancellationToken);
        return NotificationChannelResult.Delivered();
    }
}

public sealed class BrowserNotificationProvider(
    ISqlSugarRepository<UserNotificationEntity> repository,
    UserNotificationRealtimeNotifier realtimeNotifier) : INotificationChannelProvider
{
    public string Channel => NotificationChannels.Browser;
    public string Provider => NotificationProviders.BrowserWebPush;
    public ProviderCapabilities Capabilities => new();

    public async Task<NotificationChannelResult> SendAsync(
        NotificationChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(request.Destination, out var userId))
        {
            return new(
                NotificationChannelResultStatus.PermanentFailure,
                ErrorCode: "invalid_user",
                ConfirmedNotAccepted: true);
        }

        await repository.InsertAsync(new UserNotificationEntity(Guid.NewGuid())
        {
            TenantId = request.TenantId,
            UserId = userId,
            EventId = request.DeliveryId,
            Title = request.Subject ?? string.Empty,
            Body = request.Body,
            Link = request.TemplateParameters.GetValueOrDefault("link"),
            CreationTime = DateTime.UtcNow
        });
        await realtimeNotifier.PublishChangedAsync(userId, cancellationToken);
        return NotificationChannelResult.Delivered();
    }
}
