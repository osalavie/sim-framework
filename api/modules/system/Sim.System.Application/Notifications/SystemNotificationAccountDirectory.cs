using Sim.System.Application.Contracts.Notifications;
using SqlSugar;
using Sim.System.Domain.User;

namespace Sim.System.Application.Notifications;

internal sealed class SystemNotificationAccountDirectory(IUserRepository userRepository)
    : INotificationAccountDirectory
{
    public async Task<IReadOnlyList<NotificationAccount>> SearchAsync(
        string? keyword,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var query = userRepository.GetQueryable().Where(user => user.IsActive);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(user =>
                user.UserName.Contains(normalized) ||
                (user.NickName != null && user.NickName.Contains(normalized)) ||
                (user.Name != null && user.Name.Contains(normalized)) ||
                (user.Email != null && user.Email.Contains(normalized)) ||
                (user.Phone != null && user.Phone.Contains(normalized)));
        }

        var users = await query
            .OrderBy(user => user.UserName)
            .Take(Math.Clamp(maxCount, 1, 250))
            .ToListAsync(cancellationToken);

        return users.Select(Map).ToList();
    }

    public async Task<NotificationAccount?> FindAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userRepository.GetAsync(item => item.Id == userId && item.IsActive);
        return user == null ? null : Map(user);
    }

    private static NotificationAccount Map(UserEntity user) => new(
        user.Id,
        user.UserName,
        user.NickName ?? user.Name ?? user.UserName,
        user.Email,
        user.Phone);
}
