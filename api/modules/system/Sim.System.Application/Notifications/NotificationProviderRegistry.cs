using Sim.System.Notifications;

namespace Sim.System.Application.Notifications;

public sealed class NotificationProviderRegistry(IEnumerable<INotificationChannelProvider> providers)
{
    private readonly Dictionary<string, INotificationChannelProvider> _providers = providers
        .ToDictionary(item => Key(item.Channel, item.Provider), StringComparer.OrdinalIgnoreCase);

    public INotificationChannelProvider GetRequired(string channel, string provider) =>
        _providers.GetValueOrDefault(Key(channel, provider))
        ?? throw new InvalidOperationException(
            $"Notification provider '{channel}/{provider}' is not registered.");

    private static string Key(string channel, string provider) => $"{channel}:{provider}";
}
