using Microsoft.Extensions.Logging;
using Sim.System.Application.Contracts.Notifications;

namespace Sim.System.Application.Notifications;

public sealed class UserNotificationRealtimeNotifier(
    IEnumerable<IUserNotificationRealtimePublisher> publishers,
    ILogger<UserNotificationRealtimeNotifier> logger)
{
    public async Task PublishChangedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        foreach (var publisher in publishers)
        {
            try
            {
                await publisher.PublishChangedAsync(userId, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Realtime notification signal failed. The persisted inbox notification remains available.");
            }
        }
    }
}
