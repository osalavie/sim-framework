using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Sim.System.Application.Contracts.Notifications;

namespace Sim.Admin.HttpApi.Host.Notifications;

[Authorize(AuthenticationSchemes = "sim_admin_api")]
public sealed class UserNotificationHub : Hub;

public sealed class SignalRUserNotificationRealtimePublisher(
    IHubContext<UserNotificationHub> hubContext)
    : IUserNotificationRealtimePublisher
{
    public const string ChangedEvent = "notificationChanged";

    public Task PublishChangedAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        hubContext.Clients.User(userId.ToString()).SendAsync(
            ChangedEvent,
            cancellationToken);
}
