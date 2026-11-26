using Microsoft.Extensions.Configuration;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Application.Dictionary;
using Sim.System.Application.SystemConfig;
using Sim.System.Domain.Notifications;
using Sim.System.Domain.Shared.Consts;
using Sim.System.Notifications;

namespace Sim.System.Application.Notifications;

public sealed class NotificationConfigurationInitializer(
    ISqlSugarRepository<NotificationProviderConfigurationEntity> repository,
    NotificationConfigurationProtection protection,
    ISystemConfigurationReader configurationReader,
    IDataDictionaryReader dictionaryReader,
    IConfiguration configuration)
{
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        var brandName = await ResolveBrandNameAsync();
        var displayNames = await dictionaryReader.GetItemNamesAsync(
            DataDictionaryKeys.NotificationProvider);
        var existing = (await repository.GetAll())
            .ToDictionary(item => item.Provider, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in NotificationProviderDefinitions.All)
        {
            var displayName = displayNames.TryGetValue(definition.Provider, out var configuredName) &&
                              !string.IsNullOrWhiteSpace(configuredName)
                ? configuredName
                : definition.DefaultDisplayName;
            if (existing.TryGetValue(definition.Provider, out var current))
            {
                var changed = false;
                if (string.IsNullOrWhiteSpace(current.DisplayName) ||
                    string.Equals(current.DisplayName, definition.Provider, StringComparison.OrdinalIgnoreCase))
                {
                    current.DisplayName = displayName;
                    changed = true;
                }

                var defaults = DefaultSettings(definition.Provider, brandName);
                if (defaults.Count > 0 &&
                    (string.IsNullOrWhiteSpace(current.SettingsJson) ||
                     current.SettingsJson.Trim() == "{}"))
                {
                    current.SettingsJson = NotificationJson.Serialize(defaults);
                    changed = true;
                }

                if (changed)
                {
                    await repository.UpdateAsync(current);
                }

                continue;
            }

            var entity = new NotificationProviderConfigurationEntity(Guid.NewGuid())
            {
                Channel = definition.Channel,
                Provider = definition.Provider,
                DisplayName = displayName,
                Enabled = definition.Provider == NotificationProviders.Inbox,
                Priority = definition.Priority,
                SettingsJson = NotificationJson.Serialize(DefaultSettings(definition.Provider, brandName))
            };
            ApplyLegacySecurityConfiguration(entity, brandName);
            await repository.InsertAsync(entity);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<string> ResolveBrandNameAsync()
    {
        var siteName = await configurationReader.GetValueAsync(SystemConfigurationKeys.SiteName);
        return string.IsNullOrWhiteSpace(siteName) ? BrandDefaults.Name : siteName.Trim();
    }

    private Dictionary<string, string> DefaultSettings(string provider, string brandName) =>
        provider switch
        {
            NotificationProviders.Smtp => new()
            {
                ["Host"] = string.Empty,
                ["Port"] = "587",
                ["UseSsl"] = "true",
                ["SocketOptions"] = "StartTlsWhenAvailable",
                ["UserName"] = string.Empty,
                ["FromAddress"] = string.Empty,
                ["FromName"] = brandName,
                ["IsBodyHtml"] = "false"
            },
            NotificationProviders.TencentSms => new()
            {
                ["Endpoint"] = "https://sms.tencentcloudapi.com",
                ["Region"] = "ap-guangzhou",
                ["SmsSdkAppId"] = string.Empty,
                ["SignName"] = string.Empty,
                ["TemplateId"] = string.Empty,
                ["TemplateParameterOrder"] = string.Empty
            },
            NotificationProviders.AliyunSms => new()
            {
                ["Endpoint"] = "https://dysmsapi.aliyuncs.com",
                ["AccessKeyId"] = string.Empty,
                ["SignName"] = string.Empty,
                ["TemplateCode"] = string.Empty
            },
            NotificationProviders.TencentVoice => new()
            {
                ["Endpoint"] = "https://vms.tencentcloudapi.com",
                ["Region"] = "ap-guangzhou",
                ["VoiceSdkAppid"] = string.Empty,
                ["TemplateId"] = string.Empty,
                ["TemplateParameterOrder"] = string.Empty,
                ["PlayTimes"] = "1"
            },
            NotificationProviders.AliyunVoice => new()
            {
                ["Endpoint"] = "https://dyvmsapi.aliyuncs.com",
                ["AccessKeyId"] = string.Empty,
                ["CalledShowNumber"] = string.Empty,
                ["TtsCode"] = string.Empty,
                ["PlayTimes"] = "1"
            },
            NotificationProviders.StandardSms or
                NotificationProviders.StandardVoice or
                NotificationProviders.GenericWebhook or
                NotificationProviders.Feishu or
                NotificationProviders.WeCom or
                NotificationProviders.DingTalk => new()
                {
                    ["Endpoint"] = string.Empty
                },
            _ => new()
        };

    private void ApplyLegacySecurityConfiguration(
        NotificationProviderConfigurationEntity entity,
        string brandName)
    {
        if (entity.Provider == NotificationProviders.Smtp)
        {
            var section = configuration.GetSection("Security:EmailVerification");
            var settings = new Dictionary<string, string>
            {
                ["Host"] = section["Host"] ?? string.Empty,
                ["Port"] = section["Port"] ?? "587",
                ["UseSsl"] = section["UseSsl"] ?? "true",
                ["UserName"] = section["UserName"] ?? string.Empty,
                ["FromAddress"] = section["FromAddress"] ?? string.Empty,
                ["FromName"] = section["FromName"] ?? brandName,
                ["SecuritySubject"] = $"{brandName} 安全验证码",
                ["SecurityBody"] = "你的验证码是 {{code}}。请勿转发。"
            };
            entity.Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled;
            entity.SettingsJson = NotificationJson.Serialize(settings);
            if (!string.IsNullOrWhiteSpace(section["Password"]))
            {
                entity.ProtectedSecrets = protection.Protect(
                    new Dictionary<string, string> { ["Password"] = section["Password"]! });
            }
        }
        else if (entity.Provider == NotificationProviders.StandardSms)
        {
            var section = configuration.GetSection("Security:SmsVerification");
            entity.Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled;
            entity.SettingsJson = NotificationJson.Serialize(new Dictionary<string, string>
            {
                ["Endpoint"] = section["WebhookUrl"] ?? string.Empty
            });
            if (!string.IsNullOrWhiteSpace(section["AuthorizationHeader"]))
            {
                var value = section["AuthorizationHeader"]!;
                var bearer = value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? value.Substring("Bearer ".Length)
                    : value;
                entity.ProtectedSecrets = protection.Protect(
                    new Dictionary<string, string> { ["BearerToken"] = bearer });
            }
        }
    }
}
