using Scriban;
using Sim.Framework.Domain.Shared.Messaging;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.Notifications;
using Sim.System.Domain.Notifications;
using Sim.System.Notifications;
using System.Security.Cryptography;
using System.Text;

namespace Sim.System.Application.Notifications;

public sealed class NotificationEventHandler(
    ISqlSugarRepository<NotificationRuleEntity> ruleRepository,
    ISqlSugarRepository<NotificationTemplateEntity> templateRepository,
    ISqlSugarRepository<NotificationProviderConfigurationEntity> providerRepository,
    ISqlSugarRepository<NotificationWebhookEndpointEntity> webhookRepository,
    ISqlSugarRepository<NotificationDeliveryEntity> deliveryRepository,
    NotificationConfigurationProtection protection,
    NotificationAccountDestinationResolver accountDestinationResolver)
    : IMessageHandler<NotificationEvent>
{
    public async Task HandleAsync(
        NotificationEvent message,
        MessageContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rules = await ruleRepository.GetListAsync(item =>
            item.EventName == message.EventName && item.Enabled);
        foreach (var rule in rules)
        {
            var template = (await templateRepository.GetListAsync(item =>
                    item.Key == rule.TemplateKey &&
                    item.Channel == rule.Channel &&
                    item.Enabled))
                .OrderByDescending(item => item.Locale == "zh-CN")
                .FirstOrDefault();
            if (template == null)
            {
                continue;
            }

            var renderedSubject = Render(template.Subject, message.Data);
            var renderedBody = Render(template.Body, message.Data) ?? string.Empty;
            if (rule.Channel == NotificationChannels.Webhook)
            {
                var endpoints = await webhookRepository.GetListAsync(item => item.Enabled);
                foreach (var endpoint in endpoints.Where(item => Matches(item.EventNamesJson, message.EventName)))
                {
                    await CreateDeliveryAsync(
                        message,
                        rule,
                        template,
                        NotificationProviders.GenericWebhook,
                        endpoint.Url,
                        renderedSubject,
                        renderedBody,
                        endpoint.Id.ToString("N"),
                        cancellationToken);
                }

                continue;
            }

            var provider = rule.Provider;
            if (string.IsNullOrWhiteSpace(provider))
            {
                provider = (await providerRepository.GetListAsync(item =>
                        item.Channel == rule.Channel && item.Enabled))
                    .OrderBy(item => item.Priority)
                    .Select(item => item.Provider)
                    .FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                continue;
            }

            foreach (var recipient in message.Recipients.Where(item =>
                         string.IsNullOrWhiteSpace(item.Channel) ||
                         string.Equals(item.Channel, rule.Channel, StringComparison.OrdinalIgnoreCase)))
            {
                var destination = rule.Channel is NotificationChannels.Inbox or NotificationChannels.Browser
                    ? recipient.UserId?.ToString()
                    : recipient.Address;
                if (string.IsNullOrWhiteSpace(destination) && recipient.UserId.HasValue)
                {
                    destination = await accountDestinationResolver.ResolveAsync(
                        recipient.UserId.Value,
                        rule.Channel,
                        cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(destination))
                {
                    continue;
                }

                await CreateDeliveryAsync(
                    message,
                    rule,
                    template,
                    provider,
                    destination,
                    renderedSubject,
                    renderedBody,
                    null,
                    cancellationToken);
            }
        }
    }

    private async Task CreateDeliveryAsync(
        NotificationEvent message,
        NotificationRuleEntity rule,
        NotificationTemplateEntity template,
        string provider,
        string destination,
        string? subject,
        string body,
        string? endpointId,
        CancellationToken cancellationToken)
    {
        var suffix = endpointId ?? Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(destination))).ToLowerInvariant();
        var idempotencyKey = $"{message.IdempotencyKey ?? message.EventId.ToString("N")}:{rule.Id:N}:{suffix}";
        if (await deliveryRepository.GetAsync(item => item.IdempotencyKey == idempotencyKey) != null)
        {
            return;
        }

        var parameters = new Dictionary<string, string>(message.Data, StringComparer.Ordinal);
        if (endpointId != null)
        {
            parameters["_webhookEndpointId"] = endpointId;
        }

        await deliveryRepository.InsertAsync(new NotificationDeliveryEntity(Guid.NewGuid())
        {
            TenantId = message.TenantId,
            EventId = message.EventId,
            EventName = message.EventName,
            Channel = rule.Channel,
            Provider = provider,
            DestinationMasked = NotificationMask.Mask(destination),
            ProtectedDestination = protection.Protect(
                new Dictionary<string, string> { ["Destination"] = destination })!,
            Subject = subject,
            Body = body,
            TemplateCode = template.ProviderTemplateCode,
            ParametersJson = NotificationJson.Serialize(parameters),
            IdempotencyKey = idempotencyKey,
            Status = NotificationDeliveryStatuses.Created,
            NextAttemptAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = message.ExpiresAtUtc?.UtcDateTime,
            CreationTime = DateTime.UtcNow
        });
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static bool Matches(string json, string eventName)
    {
        var events = NotificationJson.DeserializeList(json);
        return events.Count == 0 || events.Contains(eventName, StringComparer.OrdinalIgnoreCase);
    }

    private static string? Render(string? source, IReadOnlyDictionary<string, string> data)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return source;
        }

        var template = Template.Parse(source);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(string.Join("; ", template.Messages.Select(item => item.Message)));
        }

        return template.Render(data, member => member.Name);
    }
}
