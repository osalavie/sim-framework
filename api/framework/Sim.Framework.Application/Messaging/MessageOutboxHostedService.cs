using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sim.Framework.Domain.Shared.Messaging;

namespace Sim.Framework.Application.Messaging;

public sealed class MessageOutboxHostedService(
    IServiceScopeFactory scopeFactory,
    IMessageTransport transport,
    MessageDispatcher dispatcher,
    IOptions<MessagingOptions> options,
    IConfiguration configuration,
    ILogger<MessageOutboxHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || configuration.GetValue<bool>("App:OpenApiGeneration"))
        {
            logger.LogInformation("Framework message dispatcher is disabled");
            return;
        }

        await Task.Delay(
            Math.Clamp(options.Value.StartupDelayMilliseconds, 0, 60000),
            stoppingToken);
        try
        {
            await transport.StartAsync(dispatcher.DispatchAsync, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "Failed to start message transport {Provider}. Outbox dispatching will stop, " +
                "but the host keeps running. Check transport configuration and connectivity. Error: {Error}",
                transport.Name,
                ex.GetBaseException().Message);
            throw;
        }

        logger.LogInformation(
            "Framework message transport started. Provider={Provider}",
            transport.Name);

        var interval = TimeSpan.FromMilliseconds(
            Math.Clamp(options.Value.PollIntervalMilliseconds, 100, 60000));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Outbox batch failed; retrying on the next poll.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await transport.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IMessageOutboxStore>();
        var pending = await store.GetPendingAsync(
            Math.Clamp(options.Value.OutboxBatchSize, 1, 1000),
            DateTimeOffset.UtcNow,
            cancellationToken);
        foreach (var item in pending)
        {
            try
            {
                await transport.PublishAsync(item.Envelope, cancellationToken);
                await store.MarkPublishedAsync(item.Id, DateTimeOffset.UtcNow, cancellationToken);
            }
            catch (Exception exception)
            {
                var delaySeconds = Math.Min(3600, Math.Pow(2, Math.Min(item.Attempts + 1, 10)));
                await store.MarkFailedAsync(
                    item.Id,
                    exception.GetBaseException().Message,
                    DateTimeOffset.UtcNow.AddSeconds(delaySeconds),
                    cancellationToken);
                logger.LogWarning(
                    exception,
                    "Outbox publish failed. MessageId={MessageId}, Attempt={Attempt}",
                    item.Envelope.MessageId,
                    item.Attempts + 1);
            }
        }
    }
}
