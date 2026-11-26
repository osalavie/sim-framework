using System.Net;
using Microsoft.AspNetCore.Authorization;
using Sim.Framework.Application.Contracts.Authorization;
using Sim.Framework.Application.Services;
using Sim.Framework.Core.UnitOfWork;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Contracts.Notifications;
using Sim.System.Domain.Notifications;
using Sim.System.Notifications;
using Sim.Framework.Domain.Shared.Exceptions;
using Sim.System.Domain.Shared;
using Scriban;
using Sim.System.Domain.Shared.ErrorCodes;

namespace Sim.System.Application.Notifications;

[Authorize]
[Permission(NotificationPermissions.Manage)]
public sealed class NotificationAdministrationService(
    ISqlSugarRepository<NotificationProviderConfigurationEntity> providerRepository,
    ISqlSugarRepository<NotificationTemplateEntity> templateRepository,
    ISqlSugarRepository<NotificationRuleEntity> ruleRepository,
    ISqlSugarRepository<NotificationWebhookEndpointEntity> webhookRepository,
    ISqlSugarRepository<NotificationDeliveryEntity> deliveryRepository,
    NotificationConfigurationProtection protection,
    NotificationProviderRegistry registry,
    NotificationAccountDestinationResolver accountDestinationResolver)
    : ApplicationService, INotificationAdministrationService
{
    private static readonly HashSet<string> Channels =
    [
        NotificationChannels.Inbox,
        NotificationChannels.Browser,
        NotificationChannels.Email,
        NotificationChannels.Sms,
        NotificationChannels.Webhook,
        NotificationChannels.Feishu,
        NotificationChannels.WeCom,
        NotificationChannels.DingTalk,
        NotificationChannels.Voice
    ];

    private static readonly Dictionary<string, string> ProviderChannels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [NotificationProviders.Inbox] = NotificationChannels.Inbox,
            [NotificationProviders.BrowserWebPush] = NotificationChannels.Browser,
            [NotificationProviders.Smtp] = NotificationChannels.Email,
            [NotificationProviders.TencentSms] = NotificationChannels.Sms,
            [NotificationProviders.AliyunSms] = NotificationChannels.Sms,
            [NotificationProviders.StandardSms] = NotificationChannels.Sms,
            [NotificationProviders.GenericWebhook] = NotificationChannels.Webhook,
            [NotificationProviders.Feishu] = NotificationChannels.Feishu,
            [NotificationProviders.WeCom] = NotificationChannels.WeCom,
            [NotificationProviders.DingTalk] = NotificationChannels.DingTalk,
            [NotificationProviders.TencentVoice] = NotificationChannels.Voice,
            [NotificationProviders.AliyunVoice] = NotificationChannels.Voice,
            [NotificationProviders.StandardVoice] = NotificationChannels.Voice
        };

    /// <summary>
    /// 获取通知管理快照（提供方、模板、规则与 Webhook）。
    /// </summary>
    public async Task<NotificationAdministrationSnapshotDto> GetAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new NotificationAdministrationSnapshotDto
        {
            Providers = (await providerRepository.GetAll()).Select(Map).ToList(),
            Templates = (await templateRepository.GetAll()).Select(Map).ToList(),
            Rules = (await ruleRepository.GetAll()).Select(Map).ToList(),
            Webhooks = (await webhookRepository.GetAll()).Select(Map).ToList()
        };
    }

    /// <summary>
    /// 保存通知提供方配置。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<NotificationProviderConfigurationDto> SaveProviderAsync(
        SaveNotificationProviderConfigurationDto input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var channel = Normalize(input.Channel);
        var provider = Normalize(input.Provider);
        if (!ProviderChannels.TryGetValue(provider, out var expectedChannel) ||
            !string.Equals(expectedChannel, channel, StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidConfiguration();
        }

        ValidateProviderSettings(input, channel);
        if (input.Settings.Keys.Any(IsSecretSettingName))
        {
            throw InvalidConfiguration();
        }
        var entity = input.Id.HasValue
            ? await providerRepository.GetAsync(input.Id.Value)
            : await providerRepository.GetAsync(item => item.Channel == channel && item.Provider == provider);
        var isNew = entity == null;
        entity ??= new NotificationProviderConfigurationEntity(Guid.NewGuid());
        entity.Channel = channel;
        entity.Provider = provider;
        entity.DisplayName = input.DisplayName.Trim();
        entity.Enabled = input.Enabled;
        entity.Priority = input.Priority;
        entity.SettingsJson = NotificationJson.Serialize(input.Settings
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key.Trim(), item => item.Value?.Trim() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase));
        if (input.ClearSecrets)
        {
            entity.ProtectedSecrets = null;
        }
        else if (input.Secrets.Count > 0)
        {
            var secrets = MergeSecrets(
                protection.Unprotect(entity.ProtectedSecrets),
                input.Secrets);
            entity.ProtectedSecrets = secrets.Count > 0
                ? protection.Protect(secrets)
                : null;
        }

        if (isNew)
        {
            await providerRepository.InsertAsync(entity);
        }
        else
        {
            await providerRepository.UpdateAsync(entity);
        }

        return Map(entity);
    }

    /// <summary>
    /// 保存通知模板。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<NotificationTemplateDto> SaveTemplateAsync(
        SaveNotificationTemplateDto input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var channel = Normalize(input.Channel);
        if (!Channels.Contains(channel))
        {
            throw InvalidConfiguration();
        }
        ValidateTemplate(input.Subject);
        ValidateTemplate(input.Body);

        var entity = input.Id.HasValue ? await templateRepository.GetAsync(input.Id.Value) : null;
        var isNew = entity == null;
        entity ??= new NotificationTemplateEntity(Guid.NewGuid());
        entity.Key = input.Key.Trim();
        entity.Channel = channel;
        entity.Locale = input.Locale.Trim();
        entity.Subject = TrimOptional(input.Subject);
        entity.Body = input.Body;
        entity.ProviderTemplateCode = NormalizeOptional(input.ProviderTemplateCode);
        entity.Enabled = input.Enabled;
        if (isNew) await templateRepository.InsertAsync(entity);
        else await templateRepository.UpdateAsync(entity);
        return Map(entity);
    }

    /// <summary>
    /// 保存通知规则。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<NotificationRuleDto> SaveRuleAsync(
        SaveNotificationRuleDto input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var channel = Normalize(input.Channel);
        if (!Channels.Contains(channel))
        {
            throw InvalidConfiguration();
        }

        var provider = NormalizeOptional(input.Provider);
        if (provider != null &&
            (!ProviderChannels.TryGetValue(provider, out var providerChannel) ||
             providerChannel != channel))
        {
            throw InvalidConfiguration();
        }

        var entity = input.Id.HasValue ? await ruleRepository.GetAsync(input.Id.Value) : null;
        var isNew = entity == null;
        entity ??= new NotificationRuleEntity(Guid.NewGuid());
        entity.EventName = input.EventName.Trim();
        entity.Channel = channel;
        entity.Provider = provider;
        entity.TemplateKey = input.TemplateKey.Trim();
        entity.Enabled = input.Enabled;
        if (isNew) await ruleRepository.InsertAsync(entity);
        else await ruleRepository.UpdateAsync(entity);
        return Map(entity);
    }

    /// <summary>
    /// 保存 Webhook 端点配置。
    /// </summary>
    [SqlSugarUnitOfWork]
    public async Task<WebhookEndpointDto> SaveWebhookAsync(
        SaveWebhookEndpointDto input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidatePublicHttpsUrl(input.Url);
        var entity = input.Id.HasValue ? await webhookRepository.GetAsync(input.Id.Value) : null;
        var isNew = entity == null;
        entity ??= new NotificationWebhookEndpointEntity(Guid.NewGuid());
        entity.Site = input.Site.Trim();
        entity.Name = input.Name.Trim();
        entity.Url = input.Url.Trim();
        entity.Enabled = input.Enabled;
        entity.EventNamesJson = NotificationJson.Serialize(input.EventNames
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList());
        if (input.ClearSigningSecret)
        {
            entity.ProtectedSigningSecret = null;
        }
        else if (!string.IsNullOrWhiteSpace(input.SigningSecret))
        {
            entity.ProtectedSigningSecret = protection.Protect(
                new Dictionary<string, string> { ["SigningSecret"] = input.SigningSecret });
        }

        if (isNew) await webhookRepository.InsertAsync(entity);
        else await webhookRepository.UpdateAsync(entity);
        return Map(entity);
    }

    /// <summary>
    /// 查询通知投递记录。
    /// </summary>
    [Permission(NotificationPermissions.ViewDeliveries)]
    public async Task<List<NotificationDeliveryDto>> GetDeliveriesAsync(
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        var items = await deliveryRepository.GetQueryable()
            .OrderBy(item => item.CreationTime, global::SqlSugar.OrderByType.Desc)
            .Take(Math.Clamp(maxCount, 1, 500))
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    /// <summary>
    /// 按渠道搜索可用于通知的账号接收人。
    /// </summary>
    public Task<List<NotificationAccountDto>> GetAccountsAsync(
        string channel,
        string? keyword = null,
        int maxCount = 20,
        CancellationToken cancellationToken = default) =>
        accountDestinationResolver.SearchAsync(
            Normalize(channel),
            keyword,
            maxCount,
            cancellationToken);

    /// <summary>
    /// 使用指定通知提供方向目标账号发送测试消息。
    /// </summary>
    public async Task<SecurityNotificationResult> TestProviderAsync(
        Guid id,
        string? destination = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await providerRepository.GetAsync(id)
                     ?? throw new BusinessException(SystemErrorCodes.NotificationProviderNotFound);
        if (userId.HasValue)
        {
            destination = await accountDestinationResolver.ResolveAsync(
                userId.Value,
                entity.Channel,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            return new(
                false,
                NotificationChannelResultStatus.PermanentFailure.ToString().ToLowerInvariant(),
                SystemErrorCodes.NotificationDestinationUnavailable);
        }

        var configuration = NotificationJson.DeserializeDictionary(entity.SettingsJson);
        foreach (var secret in protection.Unprotect(entity.ProtectedSecrets))
        {
            configuration[secret.Key] = secret.Value;
        }

        var result = await registry.GetRequired(entity.Channel, entity.Provider).SendAsync(
            new NotificationChannelRequest(
                Guid.NewGuid(),
                entity.Channel,
                entity.Provider,
                destination.Trim(),
                "Notification provider test",
                "This is a notification provider connectivity test.",
                configuration.GetValueOrDefault("SecurityTemplateCode"),
                new Dictionary<string, string> { ["code"] = "000000", ["purpose"] = "test" },
                configuration,
                Guid.NewGuid().ToString("N")),
            cancellationToken);
        return new(
            result.Status is NotificationChannelResultStatus.Accepted or NotificationChannelResultStatus.Delivered,
            result.Status.ToString().ToLowerInvariant(),
            result.ErrorCode);
    }

    private static void ValidateProviderSettings(
        SaveNotificationProviderConfigurationDto input,
        string channel)
    {
        if (channel is NotificationChannels.Sms or NotificationChannels.Voice or
            NotificationChannels.Webhook or NotificationChannels.Feishu or
            NotificationChannels.WeCom or NotificationChannels.DingTalk)
        {
            var endpoint = input.Secrets.GetValueOrDefault("Endpoint");
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = input.Settings.GetValueOrDefault("Endpoint");
            }

            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                ValidatePublicHttpsUrl(endpoint);
            }
        }
    }

    private static void ValidatePublicHttpsUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            uri.IsLoopback ||
            (IPAddress.TryParse(uri.Host, out var address) && IsPrivate(address)))
        {
            throw InvalidConfiguration();
        }
    }

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return IPAddress.IsLoopback(address) ||
               (address.AddressFamily == global::System.Net.Sockets.AddressFamily.InterNetwork &&
                (bytes[0] == 10 ||
                 bytes[0] == 127 ||
                 (bytes[0] == 169 && bytes[1] == 254) ||
                 (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                 (bytes[0] == 192 && bytes[1] == 168)));
    }

    private NotificationProviderConfigurationDto Map(NotificationProviderConfigurationEntity item) => new()
    {
        Id = item.Id,
        Channel = item.Channel,
        Provider = item.Provider,
        DisplayName = item.DisplayName,
        Enabled = item.Enabled,
        Priority = item.Priority,
        Settings = NotificationJson.DeserializeDictionary(item.SettingsJson),
        HasSecret = !string.IsNullOrWhiteSpace(item.ProtectedSecrets)
    };

    private static NotificationTemplateDto Map(NotificationTemplateEntity item) => new()
    {
        Id = item.Id, Key = item.Key, Channel = item.Channel, Locale = item.Locale,
        Subject = item.Subject, Body = item.Body,
        ProviderTemplateCode = item.ProviderTemplateCode, Enabled = item.Enabled
    };

    private static NotificationRuleDto Map(NotificationRuleEntity item) => new()
    {
        Id = item.Id, EventName = item.EventName, Channel = item.Channel,
        Provider = item.Provider, TemplateKey = item.TemplateKey, Enabled = item.Enabled
    };

    private static WebhookEndpointDto Map(NotificationWebhookEndpointEntity item) => new()
    {
        Id = item.Id, Site = item.Site, Name = item.Name, Url = item.Url,
        Enabled = item.Enabled, EventNames = NotificationJson.DeserializeList(item.EventNamesJson),
        HasSecret = !string.IsNullOrWhiteSpace(item.ProtectedSigningSecret)
    };

    private static NotificationDeliveryDto Map(NotificationDeliveryEntity item) => new()
    {
        Id = item.Id, EventId = item.EventId, EventName = item.EventName,
        Channel = item.Channel, Provider = item.Provider,
        DestinationMasked = item.DestinationMasked, Status = item.Status,
        Attempts = item.Attempts, ErrorCode = item.ErrorCode,
        CreationTime = item.CreationTime, LastAttemptAtUtc = item.LastAttemptAtUtc
    };

    private static bool IsSecretSettingName(string value)
    {
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("privatekey", StringComparison.OrdinalIgnoreCase);
    }

    internal static Dictionary<string, string> MergeSecrets(
        IReadOnlyDictionary<string, string> existing,
        IReadOnlyDictionary<string, string> updates)
    {
        var merged = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
        foreach (var secret in updates.Where(item =>
                     !string.IsNullOrWhiteSpace(item.Key) &&
                     !string.IsNullOrWhiteSpace(item.Value)))
        {
            merged[secret.Key.Trim()] = secret.Value;
        }

        return merged;
    }

    private static void ValidateTemplate(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        if (Template.Parse(source).HasErrors)
        {
            throw InvalidConfiguration();
        }
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    private static string? TrimOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    private static BusinessException InvalidConfiguration() =>
        new(SystemErrorCodes.InvalidNotificationConfiguration);
}
