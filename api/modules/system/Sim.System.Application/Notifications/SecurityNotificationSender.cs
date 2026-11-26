using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.Notifications;
using Sim.System.Domain.Notifications;
using Sim.System.Notifications;

namespace Sim.System.Application.Notifications;

public sealed class SecurityNotificationSender(
    ISqlSugarRepository<NotificationProviderConfigurationEntity> providerRepository,
    NotificationConfigurationProtection protection,
    NotificationProviderRegistry registry) : ISecurityNotificationSender
{
    public async Task<SecurityNotificationResult> SendAsync(
        SecurityNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        var channel = command.Channel.Trim().ToLowerInvariant();
        if (channel is not (NotificationChannels.Email or NotificationChannels.Sms))
        {
            return new(false, NotificationDeliveryStatuses.Failed, "unsupported_channel");
        }

        var provider = (await providerRepository.GetListAsync(item =>
                item.Channel == channel && item.Enabled))
            .OrderBy(item => item.Priority)
            .FirstOrDefault();
        if (provider == null)
        {
            return new(false, NotificationDeliveryStatuses.Failed, "provider_not_configured");
        }

        var configuration = NotificationJson.DeserializeDictionary(provider.SettingsJson);
        foreach (var secret in protection.Unprotect(provider.ProtectedSecrets))
        {
            configuration[secret.Key] = secret.Value;
        }

        var subject = configuration.GetValueOrDefault("SecuritySubject") ?? "Security verification";
        var bodyTemplate = configuration.GetValueOrDefault("SecurityBody")
                           ?? (channel == NotificationChannels.Email
                               ? "Your verification code is {{code}}."
                               : "{{code}}");
        var body = bodyTemplate.Replace("{{code}}", command.Code, StringComparison.Ordinal);
        var result = await registry.GetRequired(channel, provider.Provider).SendAsync(
            new NotificationChannelRequest(
                Guid.NewGuid(),
                channel,
                provider.Provider,
                command.Destination,
                subject,
                body,
                configuration.GetValueOrDefault("SecurityTemplateCode"),
                new Dictionary<string, string> { ["code"] = command.Code, ["purpose"] = command.Purpose },
                configuration,
                Guid.NewGuid().ToString("N")),
            cancellationToken);
        return new(
            result.Status is NotificationChannelResultStatus.Accepted or NotificationChannelResultStatus.Delivered,
            result.Status.ToString().ToLowerInvariant(),
            result.ErrorCode);
    }
}
