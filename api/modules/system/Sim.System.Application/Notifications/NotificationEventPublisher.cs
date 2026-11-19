using Sim.Framework.Domain.Shared.Messaging;
using Sim.System.Application.Contracts.Notifications;
using Sim.System.Notifications;

namespace Sim.System.Application.Notifications;

public sealed class NotificationEventPublisher(IMessageBus messageBus) : INotificationEventPublisher
{
    public Task PublishAsync(
        NotificationEvent notificationEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationEvent.EventName);
        notificationEvent.IdempotencyKey ??= notificationEvent.EventId.ToString("N");
        return messageBus.PublishAsync(
            notificationEvent,
            new MessagePublishOptions
            {
                Type = NotificationMessageTypes.Event,
                Version = notificationEvent.Version,
                CorrelationId = notificationEvent.CorrelationId,
                CausationId = notificationEvent.CausationId,
                TenantId = notificationEvent.TenantId,
                PartitionKey = notificationEvent.TenantId?.ToString("N")
            },
            cancellationToken);
    }
}
