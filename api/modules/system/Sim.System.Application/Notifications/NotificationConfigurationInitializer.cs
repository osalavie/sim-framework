using Microsoft.Extensions.Configuration;
using Sim.SqlSugar.Core.Repositories;
using Sim.System.Domain.Notifications;
using Sim.System.Notifications;

namespace Sim.System.Application.Notifications;

public sealed class NotificationConfigurationInitializer(
    ISqlSugarRepository<NotificationProviderConfigurationEntity> repository,
    NotificationConfigurationProtection protection,
    IConfiguration configuration)
{
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        var existing = (await repository.GetAll())
            .ToDictionary(item => item.Provider, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            if (existing.TryGetValue(definition.Provider, out var current))
            {
                var defaults = DefaultSettings(definition.Provider);
                if (defaults.Count > 0 &&
                    (string.IsNullOrWhiteSpace(current.SettingsJson) ||
                     current.SettingsJson.Trim() == "{}"))
                {
                    current.SettingsJson = NotificationJson.Serialize(defaults);
                    await repository.UpdateAsync(current);
                }

                continue;
            }

            var entity = new NotificationProviderConfigurationEntity(Guid.NewGuid())
            {
                Channel = definition.Channel,
                Provider = definition.Provider,
                DisplayName = definition.DisplayName,
                Enabled = definition.Provider == NotificationProviders.Inbox,
                Priority = definition.Priority,
                SettingsJson = NotificationJson.Serialize(DefaultSettings(definition.Provider))
            };
            ApplyLegacySecurityConfiguration(entity);
            await repository.InsertAsync(entity);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static Dictionary<string, string> DefaultSettings(string provider) =>
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
                ["FromName"] = "SimFramework",
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

    private void ApplyLegacySecurityConfiguration(NotificationProviderConfigurationEntity entity)
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
                ["FromName"] = section["FromName"] ?? "SimFramework",
                ["SecuritySubject"] = "SimFramework 安全验证码",
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

    private static readonly (string Channel, string Provider, string DisplayName, int Priority)[] Definitions =
    [
        (NotificationChannels.Inbox, NotificationProviders.Inbox, "站内通知", 0),
        (NotificationChannels.Browser, NotificationProviders.BrowserWebPush, "浏览器通知", 10),
        (NotificationChannels.Email, NotificationProviders.Smtp, "SMTP 邮件", 100),
        (NotificationChannels.Sms, NotificationProviders.TencentSms, "腾讯云短信", 100),
        (NotificationChannels.Sms, NotificationProviders.AliyunSms, "阿里云短信", 110),
        (NotificationChannels.Sms, NotificationProviders.StandardSms, "标准 HTTP 短信", 200),
        (NotificationChannels.Webhook, NotificationProviders.GenericWebhook, "通用 Webhook", 100),
        (NotificationChannels.Feishu, NotificationProviders.Feishu, "飞书机器人", 100),
        (NotificationChannels.WeCom, NotificationProviders.WeCom, "企业微信机器人", 100),
        (NotificationChannels.DingTalk, NotificationProviders.DingTalk, "钉钉机器人", 100),
        (NotificationChannels.Voice, NotificationProviders.TencentVoice, "腾讯云语音", 100),
        (NotificationChannels.Voice, NotificationProviders.AliyunVoice, "阿里云语音", 110),
        (NotificationChannels.Voice, NotificationProviders.StandardVoice, "标准 HTTP 语音", 200)
    ];
}
