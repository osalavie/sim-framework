using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Services;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.Notifications;
using Sim.System.Domain.Notifications;
using Sim.Framework.Domain.Shared.Security;

namespace Sim.System.Application.Notifications;

[Authorize]
public sealed class UserNotificationService(
    ISqlSugarRepository<UserNotificationEntity> repository,
    ICurrentUser currentUser,
    UserNotificationRealtimeNotifier realtimeNotifier)
    : ApplicationService, IUserNotificationService
{
    /// <summary>
    /// 获取当前用户的通知摘要（未读数量与最近通知）。
    /// </summary>
    public async Task<UserNotificationSummaryDto> GetAsync(
        int maxCount = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id ?? throw new UnauthorizedAccessException();
        maxCount = Math.Clamp(maxCount, 1, 100);
        var query = repository.GetQueryable().Where(item => item.UserId == userId);
        var unread = await query.Clone().CountAsync(item => !item.IsRead, cancellationToken);
        var items = await query.OrderBy(item => item.CreationTime, global::SqlSugar.OrderByType.Desc)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
        return new UserNotificationSummaryDto(
            unread,
            items.Select(item => new UserNotificationDto
            {
                Id = item.Id,
                Title = item.Title,
                Body = item.Body,
                Link = item.Link,
                IsRead = item.IsRead,
                CreationTime = item.CreationTime
            }).ToList());
    }

    /// <summary>
    /// 将指定通知标记为已读。
    /// </summary>
    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = currentUser.Id ?? throw new UnauthorizedAccessException();
        var entity = await repository.GetAsync(item => item.Id == id && item.UserId == userId);
        if (entity == null)
        {
            return;
        }

        entity.IsRead = true;
        entity.ReadAtUtc = DateTime.UtcNow;
        await repository.UpdateAsync(entity);
        await realtimeNotifier.PublishChangedAsync(userId, cancellationToken);
    }

    /// <summary>
    /// 将当前用户的全部通知标记为已读。
    /// </summary>
    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var userId = currentUser.Id ?? throw new UnauthorizedAccessException();
        var items = await repository.GetListAsync(item => item.UserId == userId && !item.IsRead);
        foreach (var item in items)
        {
            item.IsRead = true;
            item.ReadAtUtc = DateTime.UtcNow;
        }

        if (items.Count > 0)
        {
            await repository.UpdateManyAsync(items);
            await realtimeNotifier.PublishChangedAsync(userId, cancellationToken);
        }
    }
}
