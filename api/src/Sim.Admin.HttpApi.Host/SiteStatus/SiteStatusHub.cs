using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Sim.Admin.HttpApi.Host.SiteStatus;

/// <summary>
/// 匿名站点状态通道：Web 客户端在页面生命周期内保持连接，维护开关变化时即时下发。
/// 维护模式期间仍可连接，客户端因此能在维护结束后自动恢复。
/// </summary>
[AllowAnonymous]
public sealed class SiteStatusHub(IConfiguration configuration) : Hub
{
    public const string ChangedEvent = "siteStatusChanged";

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync(ChangedEvent, SiteStatusSnapshot.Read(configuration));
    }
}

/// <summary>
/// 站点状态推送载荷。显式使用小驼峰字段名，避免客户端因序列化配置差异解析失败。
/// </summary>
public sealed record SiteStatusMessage(
    [property: JsonPropertyName("maintenanceMode")] bool MaintenanceMode,
    [property: JsonPropertyName("maintenanceMessage")] string? MaintenanceMessage);

/// <summary>读取当前部署级站点状态。</summary>
public static class SiteStatusSnapshot
{
    public static SiteStatusMessage Read(IConfiguration configuration)
    {
        var message = configuration["Site:MaintenanceMessage"];
        return new SiteStatusMessage(
            configuration.GetValue("Site:MaintenanceMode", false),
            string.IsNullOrWhiteSpace(message) ? null : message);
    }
}

/// <summary>
/// 监听可热加载的 maintenance.json，变更后向所有已连接客户端广播站点状态。
/// 状态接口与 503 拦截仍保留，用于长连接不可用时的兜底。
/// </summary>
public sealed class SiteStatusRealtimeBroadcaster(
    IConfiguration configuration,
    IHubContext<SiteStatusHub> hubContext,
    ILogger<SiteStatusRealtimeBroadcaster> logger) : IHostedService, IDisposable
{
    private IDisposable? subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        subscription = ChangeToken.OnChange(
            configuration.GetReloadToken,
            () =>
            {
                _ = PublishAsync();
            });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        subscription?.Dispose();
        subscription = null;
        return Task.CompletedTask;
    }

    public void Dispose() => subscription?.Dispose();

    private async Task PublishAsync()
    {
        try
        {
            await hubContext.Clients.All.SendAsync(
                SiteStatusHub.ChangedEvent,
                SiteStatusSnapshot.Read(configuration));
        }
        catch (Exception exception)
        {
            // 实时推送是尽力而为：失败不影响状态接口与业务请求的 503 拦截。
            logger.LogWarning(exception, "站点状态实时广播失败");
        }
    }
}
