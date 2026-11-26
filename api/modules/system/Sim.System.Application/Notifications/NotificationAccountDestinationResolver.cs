using Sim.System.Application.Contracts.Notifications;
using Sim.System.Notifications;

namespace Sim.System.Application.Notifications;

public sealed class NotificationAccountDestinationResolver(
    IEnumerable<INotificationAccountDirectory> accountDirectories)
{
    private static readonly HashSet<string> SupportedChannels =
    [
        NotificationChannels.Inbox,
        NotificationChannels.Browser,
        NotificationChannels.Email,
        NotificationChannels.Sms,
        NotificationChannels.Voice
    ];

    private readonly INotificationAccountDirectory? accountDirectory =
        accountDirectories.FirstOrDefault();

    public bool Supports(string channel) => SupportedChannels.Contains(channel);

    public async Task<string?> ResolveAsync(
        Guid userId,
        string channel,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(channel) || accountDirectory == null)
        {
            return null;
        }

        var account = await accountDirectory.FindAsync(userId, cancellationToken);
        return account == null ? null : SelectDestination(account, channel);
    }

    public async Task<List<NotificationAccountDto>> SearchAsync(
        string channel,
        string? keyword,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        if (!Supports(channel) || accountDirectory == null)
        {
            return [];
        }

        var take = Math.Clamp(maxCount, 1, 50);
        var candidates = await accountDirectory.SearchAsync(
            keyword,
            Math.Min(take * 5, 250),
            cancellationToken);

        return candidates
            .Select(account => new
            {
                Account = account,
                Destination = SelectDestination(account, channel)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Destination))
            .Take(take)
            .Select(item => new NotificationAccountDto
            {
                UserId = item.Account.UserId,
                Account = item.Account.Account,
                DisplayName = item.Account.DisplayName,
                DestinationMasked = NotificationMask.Mask(item.Destination!)
            })
            .ToList();
    }

    private static string? SelectDestination(NotificationAccount account, string channel) =>
        channel switch
        {
            NotificationChannels.Inbox or NotificationChannels.Browser =>
                account.UserId.ToString(),
            NotificationChannels.Email => account.Email,
            NotificationChannels.Sms or NotificationChannels.Voice => account.Phone,
            _ => null
        };
}
