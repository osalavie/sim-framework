using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sim.Framework.Application.Caching;
using Sim.Framework.Domain.Shared.Caching;
using Sim.System.Application.Contracts.Notifications;
using Sim.System.Application.Notifications;
using Sim.System.Application.Notifications.Providers;
using Sim.System.Notifications;

namespace Sim.Security.Tests;

public class NotificationPlatformTests
{
    [Fact]
    public async Task Realtime_notification_failure_should_not_fail_persisted_delivery()
    {
        var successfulPublisher = new FakeRealtimePublisher();
        var notifier = new UserNotificationRealtimeNotifier(
            [new ThrowingRealtimePublisher(), successfulPublisher],
            NullLogger<UserNotificationRealtimeNotifier>.Instance);

        await notifier.PublishChangedAsync(Guid.NewGuid());

        Assert.Equal(1, successfulPublisher.CallCount);
    }

    [Fact]
    public async Task Account_destination_should_resolve_by_notification_channel()
    {
        var userId = Guid.NewGuid();
        var resolver = new NotificationAccountDestinationResolver(
            [new FakeNotificationAccountDirectory(
                new NotificationAccount(
                    userId,
                    "admin",
                    "Administrator",
                    "admin@example.com",
                    "13800000000"))]);

        Assert.Equal(
            "admin@example.com",
            await resolver.ResolveAsync(userId, NotificationChannels.Email));
        Assert.Equal(
            "13800000000",
            await resolver.ResolveAsync(userId, NotificationChannels.Sms));
        Assert.Equal(
            userId.ToString(),
            await resolver.ResolveAsync(userId, NotificationChannels.Inbox));
    }

    [Fact]
    public async Task Account_search_should_mask_destinations_and_filter_missing_contacts()
    {
        var resolver = new NotificationAccountDestinationResolver(
        [
            new FakeNotificationAccountDirectory(
                new NotificationAccount(
                    Guid.NewGuid(),
                    "admin",
                    "Administrator",
                    "admin@example.com",
                    "13800000000"),
                new NotificationAccount(
                    Guid.NewGuid(),
                    "without-email",
                    null,
                    null,
                    "13900000000"))
        ]);

        var accounts = await resolver.SearchAsync(
            NotificationChannels.Email,
            null,
            20);

        var account = Assert.Single(accounts);
        Assert.Equal("admin", account.Account);
        Assert.Equal("a***@example.com", account.DestinationMasked);
    }

    [Fact]
    public void Secret_updates_should_merge_without_overwriting_unchanged_credentials()
    {
        var merged = NotificationAdministrationService.MergeSecrets(
            new Dictionary<string, string>
            {
                ["SecretId"] = "existing-id",
                ["SecretKey"] = "existing-key"
            },
            new Dictionary<string, string>
            {
                ["SecretKey"] = "rotated-key",
                ["Endpoint"] = "https://example.com/hook",
                ["Ignored"] = " "
            });

        Assert.Equal("existing-id", merged["SecretId"]);
        Assert.Equal("rotated-key", merged["SecretKey"]);
        Assert.Equal("https://example.com/hook", merged["Endpoint"]);
        Assert.DoesNotContain("Ignored", merged);
    }

    [Fact]
    public async Task Local_cache_should_coalesce_concurrent_factory_calls()
    {
        using var cache = new LocalCache();
        var calls = 0;
        var tasks = Enumerable.Range(0, 20).Select(_ =>
            cache.GetOrCreateAsync(
                "notification-template",
                "welcome:zh-CN",
                async cancellationToken =>
                {
                    Interlocked.Increment(ref calls);
                    await Task.Delay(10, cancellationToken);
                    return "rendered";
                },
                new CacheEntryOptions { Expiration = TimeSpan.FromMinutes(1) }));

        var values = await Task.WhenAll(tasks);

        Assert.All(values, value => Assert.Equal("rendered", value));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Local_cache_should_honor_cached_null_policy()
    {
        using var cache = new LocalCache();
        var calls = 0;
        var options = new CacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(1),
            CacheNull = true
        };

        _ = await cache.GetOrCreateAsync<string>(
            "notification-provider",
            "missing",
            _ =>
            {
                calls++;
                return Task.FromResult<string?>(null);
            },
            options);
        _ = await cache.GetOrCreateAsync<string>(
            "notification-provider",
            "missing",
            _ =>
            {
                calls++;
                return Task.FromResult<string?>(null);
            },
            options);

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Tencent_sms_should_map_official_sdk_request_without_exposing_secret()
    {
        var channelRequest = new NotificationChannelRequest(
            Guid.NewGuid(),
            NotificationChannels.Sms,
            NotificationProviders.TencentSms,
            "+8613800000000",
            null,
            "123456",
            "1234",
            new Dictionary<string, string> { ["code"] = "123456" },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SecretId"] = "secret-id",
                ["SecretKey"] = "do-not-leak",
                ["Region"] = "ap-guangzhou",
                ["SmsSdkAppId"] = "1400000000",
                ["SignName"] = "SimApp",
                ["TemplateId"] = "1234"
            },
            "security-test");

        var sdkRequest = TencentSmsNotificationProvider.CreateRequest(channelRequest);
        var serialized = JsonSerializer.Serialize(sdkRequest);

        Assert.Equal(["+8613800000000"], sdkRequest.PhoneNumberSet);
        Assert.Equal("1400000000", sdkRequest.SmsSdkAppId);
        Assert.Equal("1234", sdkRequest.TemplateId);
        Assert.Equal(["123456"], sdkRequest.TemplateParamSet);
        Assert.Equal("security-test", sdkRequest.SessionContext);
        Assert.DoesNotContain("secret-id", serialized);
        Assert.DoesNotContain("do-not-leak", serialized);
    }

    [Fact]
    public void Aliyun_sms_should_map_official_sdk_request_without_exposing_secret()
    {
        var channelRequest = new NotificationChannelRequest(
            Guid.NewGuid(),
            NotificationChannels.Sms,
            NotificationProviders.AliyunSms,
            "13800000000",
            null,
            "123456",
            "SMS_1234",
            new Dictionary<string, string> { ["code"] = "123456" },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AccessKeyId"] = "access-id",
                ["AccessKeySecret"] = "do-not-leak",
                ["SignName"] = "SimApp",
                ["TemplateCode"] = "SMS_1234"
            },
            "security-test");

        var sdkRequest = AliyunSmsNotificationProvider.CreateRequest(channelRequest);
        var serialized = JsonSerializer.Serialize(sdkRequest);

        Assert.Equal("13800000000", sdkRequest.PhoneNumbers);
        Assert.Equal("SimApp", sdkRequest.SignName);
        Assert.Equal("SMS_1234", sdkRequest.TemplateCode);
        Assert.Contains("123456", sdkRequest.TemplateParam);
        Assert.DoesNotContain("access-id", serialized);
        Assert.DoesNotContain("do-not-leak", serialized);
    }

    [Fact]
    public void Tencent_voice_should_map_official_sdk_request()
    {
        var channelRequest = new NotificationChannelRequest(
            Guid.NewGuid(),
            NotificationChannels.Voice,
            NotificationProviders.TencentVoice,
            "+8613800000000",
            null,
            "voice body",
            "template-1",
            new Dictionary<string, string> { ["name"] = "Sim" },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["VoiceSdkAppid"] = "1400000000",
                ["TemplateId"] = "template-1",
                ["TemplateParameterOrder"] = "name"
            },
            "voice-security-test");

        var sdkRequest = TencentVoiceNotificationProvider.CreateRequest(channelRequest);

        Assert.Equal("+8613800000000", sdkRequest.CalledNumber);
        Assert.Equal("template-1", sdkRequest.TemplateId);
        Assert.Equal(["Sim"], sdkRequest.TemplateParamSet);
        Assert.Equal("voice-security-test", sdkRequest.SessionContext);
    }

    [Fact]
    public void Aliyun_voice_should_map_official_sdk_request()
    {
        var channelRequest = new NotificationChannelRequest(
            Guid.NewGuid(),
            NotificationChannels.Voice,
            NotificationProviders.AliyunVoice,
            "13800000000",
            null,
            "voice body",
            "TTS_1234",
            new Dictionary<string, string> { ["name"] = "Sim" },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CalledShowNumber"] = "057100000000",
                ["TtsCode"] = "TTS_1234",
                ["PlayTimes"] = "2"
            },
            "voice-security-test");

        var sdkRequest = AliyunVoiceNotificationProvider.CreateRequest(channelRequest);

        Assert.Equal("13800000000", sdkRequest.CalledNumber);
        Assert.Equal("057100000000", sdkRequest.CalledShowNumber);
        Assert.Equal("TTS_1234", sdkRequest.TtsCode);
        Assert.Equal(2, sdkRequest.PlayTimes);
        Assert.Contains("Sim", sdkRequest.TtsParam);
    }

    private sealed class FakeNotificationAccountDirectory(
        params NotificationAccount[] accounts) : INotificationAccountDirectory
    {
        public Task<IReadOnlyList<NotificationAccount>> SearchAsync(
            string? keyword,
            int maxCount,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<NotificationAccount> result = accounts
                .Where(account =>
                    string.IsNullOrWhiteSpace(keyword) ||
                    account.Account.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .Take(maxCount)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<NotificationAccount?> FindAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(accounts.FirstOrDefault(account => account.UserId == userId));
    }

    private sealed class FakeRealtimePublisher : IUserNotificationRealtimePublisher
    {
        public int CallCount { get; private set; }

        public Task PublishChangedAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingRealtimePublisher : IUserNotificationRealtimePublisher
    {
        public Task PublishChangedAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("realtime unavailable");
    }
}
