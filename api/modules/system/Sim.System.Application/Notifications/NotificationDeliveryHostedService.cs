using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Notifications;
using Sim.System.Notifications;
using SqlSugar;

namespace Sim.System.Application.Notifications;

public sealed class NotificationDeliveryHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<NotificationDeliveryHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (configuration.GetValue<bool>("App:OpenApiGeneration") ||
            !configuration.GetValue("System:Notifications:DeliveryEnabled", true))
        {
            return;
        }

        await Task.Delay(
            Math.Clamp(
                configuration.GetValue("System:Notifications:StartupDelayMilliseconds", 3000),
                0,
                60000),
            stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            do
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Notification delivery batch failed; retrying on the next tick.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var deliveries = scope.ServiceProvider
            .GetRequiredService<ISqlSugarRepository<NotificationDeliveryEntity>>();
        var leaseExpiredAt = DateTime.UtcNow.AddMinutes(-5);
        var due = await deliveries.GetQueryable()
            .Where(item =>
                ((item.Status == NotificationDeliveryStatuses.Created ||
                  item.Status == NotificationDeliveryStatuses.RetryScheduled) &&
                 item.NextAttemptAtUtc <= DateTime.UtcNow) ||
                (item.Status == NotificationDeliveryStatuses.Sending &&
                 item.LastAttemptAtUtc <= leaseExpiredAt))
            .OrderBy(item => item.NextAttemptAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);
        foreach (var delivery in due)
        {
            await SendAsync(scope.ServiceProvider, delivery, cancellationToken);
        }
    }

    private async Task SendAsync(
        IServiceProvider services,
        NotificationDeliveryEntity delivery,
        CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<ISqlSugarRepository<NotificationDeliveryEntity>>();
        var database = services.GetRequiredService<ISqlSugarClient>();
        if (delivery.ExpiresAtUtc <= DateTime.UtcNow)
        {
            delivery.Status = NotificationDeliveryStatuses.Expired;
            await repository.UpdateAsync(delivery);
            return;
        }

        var previousStatus = delivery.Status;
        var attemptAtUtc = DateTime.UtcNow;
        var leaseExpiredAt = attemptAtUtc.AddMinutes(-5);
        var claimed = await database.Updateable<NotificationDeliveryEntity>()
            .SetColumns(item => new NotificationDeliveryEntity
            {
                Status = NotificationDeliveryStatuses.Sending,
                Attempts = item.Attempts + 1,
                LastAttemptAtUtc = attemptAtUtc
            })
            .Where(item => item.Id == delivery.Id &&
                           item.Status == previousStatus &&
                           ((item.Status == NotificationDeliveryStatuses.Sending &&
                             item.LastAttemptAtUtc <= leaseExpiredAt) ||
                            (item.Status != NotificationDeliveryStatuses.Sending &&
                             item.NextAttemptAtUtc <= attemptAtUtc)))
            .ExecuteCommandAsync(cancellationToken);
        if (claimed == 0)
        {
            return;
        }

        delivery.Status = NotificationDeliveryStatuses.Sending;
        delivery.Attempts++;
        delivery.LastAttemptAtUtc = attemptAtUtc;

        NotificationChannelResult result;
        try
        {
            var configuration = await ResolveConfigurationAsync(services, delivery);
            var destination = services.GetRequiredService<NotificationConfigurationProtection>()
                .Unprotect(delivery.ProtectedDestination)
                .GetValueOrDefault("Destination")
                ?? throw new InvalidOperationException("Notification destination is not available.");
            var request = new NotificationChannelRequest(
                delivery.Id,
                delivery.Channel,
                delivery.Provider,
                destination,
                delivery.Subject,
                delivery.Body,
                delivery.TemplateCode,
                NotificationJson.DeserializeDictionary(delivery.ParametersJson),
                configuration,
                delivery.IdempotencyKey,
                delivery.TenantId);
            result = await services.GetRequiredService<NotificationProviderRegistry>()
                .GetRequired(delivery.Channel, delivery.Provider)
                .SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Notification delivery provider failed. DeliveryId={DeliveryId}, Channel={Channel}, Provider={Provider}",
                delivery.Id,
                delivery.Channel,
                delivery.Provider);
            result = new NotificationChannelResult(
                NotificationChannelResultStatus.TransientFailure,
                ErrorCode: "provider_exception",
                ErrorMessage: exception.GetBaseException().Message,
                ConfirmedNotAccepted: true);
        }

        delivery.ProviderRequestId = result.ProviderRequestId;
        delivery.ErrorCode = result.ErrorCode;
        delivery.ErrorMessage = Truncate(result.ErrorMessage);
        delivery.Status = result.Status switch
        {
            NotificationChannelResultStatus.Delivered => NotificationDeliveryStatuses.Delivered,
            NotificationChannelResultStatus.Accepted => NotificationDeliveryStatuses.Accepted,
            NotificationChannelResultStatus.Unknown => NotificationDeliveryStatuses.Unknown,
            NotificationChannelResultStatus.PermanentFailure => NotificationDeliveryStatuses.Failed,
            _ when delivery.Attempts < 5 => NotificationDeliveryStatuses.RetryScheduled,
            _ => NotificationDeliveryStatuses.Failed
        };
        if (delivery.Status == NotificationDeliveryStatuses.RetryScheduled)
        {
            var seconds = new[] { 30, 120, 600, 1800, 7200 }[
                Math.Min(delivery.Attempts - 1, 4)];
            delivery.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(seconds);
        }

        await repository.UpdateAsync(delivery);
    }

    private static async Task<Dictionary<string, string>> ResolveConfigurationAsync(
        IServiceProvider services,
        NotificationDeliveryEntity delivery)
    {
        var providers = services.GetRequiredService<
            ISqlSugarRepository<NotificationProviderConfigurationEntity>>();
        var entity = await providers.GetAsync(item =>
            item.Channel == delivery.Channel &&
            item.Provider == delivery.Provider &&
            item.Enabled);
        var settings = NotificationJson.DeserializeDictionary(entity?.SettingsJson);
        if (entity != null)
        {
            foreach (var secret in services.GetRequiredService<NotificationConfigurationProtection>()
                         .Unprotect(entity.ProtectedSecrets))
            {
                settings[secret.Key] = secret.Value;
            }
        }

        if (delivery.Channel == NotificationChannels.Webhook)
        {
            var endpointId = NotificationJson.DeserializeDictionary(delivery.ParametersJson)
                .GetValueOrDefault("_webhookEndpointId");
            if (Guid.TryParse(endpointId, out var id))
            {
                var endpoints = services.GetRequiredService<
                    ISqlSugarRepository<NotificationWebhookEndpointEntity>>();
                var endpoint = await endpoints.GetAsync(id);
                if (endpoint != null)
                {
                    settings["Endpoint"] = endpoint.Url;
                }
                var secret = services.GetRequiredService<NotificationConfigurationProtection>()
                    .Unprotect(endpoint?.ProtectedSigningSecret)
                    .GetValueOrDefault("SigningSecret");
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    settings["SigningSecret"] = secret;
                }
            }
        }

        return settings;
    }

    private static string? Truncate(string? value) =>
        value is { Length: > 2048 } ? value.Substring(0, 2048) : value;
}
