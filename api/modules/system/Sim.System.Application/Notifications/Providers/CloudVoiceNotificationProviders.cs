using System.Text.Json;
using AlibabaCloud.OpenApiClient.Models;
using Sim.System.Notifications;
using TencentCloud.Common;
using TencentCloud.Common.Profile;
using TencentCloud.Vms.V20200902;
using TencentCloud.Vms.V20200902.Models;
using Tea;
using AliyunSingleCallByTtsRequest =
    AlibabaCloud.SDK.Dyvmsapi20170525.Models.SingleCallByTtsRequest;
using AliyunVoiceClient = AlibabaCloud.SDK.Dyvmsapi20170525.Client;
using TencentSendTtsVoiceRequest =
    TencentCloud.Vms.V20200902.Models.SendTtsVoiceRequest;

namespace Sim.System.Application.Notifications.Providers;

public sealed class TencentVoiceNotificationProvider : INotificationChannelProvider
{
    public string Channel => NotificationChannels.Voice;
    public string Provider => NotificationProviders.TencentVoice;
    public ProviderCapabilities Capabilities => new(true, true);

    public async Task<NotificationChannelResult> SendAsync(
        NotificationChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var credential = new Credential
            {
                SecretId = Required(request.Configuration, "SecretId"),
                SecretKey = Required(request.Configuration, "SecretKey")
            };
            var region = request.Configuration.GetValueOrDefault("Region") ?? "ap-guangzhou";
            var profile = new ClientProfile
            {
                HttpProfile = new HttpProfile
                {
                    Endpoint = ResolveEndpoint(
                        request.Configuration.GetValueOrDefault("Endpoint"),
                        "vms.tencentcloudapi.com"),
                    Timeout = 15
                }
            };
            var client = new VmsClient(credential, region, profile);
            var response = await client.SendTtsVoice(CreateRequest(request))
                .WaitAsync(cancellationToken);
            return NotificationChannelResult.Accepted(
                response.SendStatus?.CallId ?? response.RequestId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(NotificationChannelResultStatus.Unknown, ErrorCode: "timeout");
        }
        catch (TencentCloudSDKException exception)
        {
            return Failure(exception.ErrorCode, exception.Message, exception.RequestId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(
                NotificationChannelResultStatus.PermanentFailure,
                ErrorCode: "tencent_voice_configuration_error",
                ErrorMessage: exception.Message,
                ConfirmedNotAccepted: true);
        }
    }

    internal static TencentSendTtsVoiceRequest CreateRequest(
        NotificationChannelRequest request) => new()
    {
        TemplateId = request.TemplateCode ?? Required(request.Configuration, "TemplateId"),
        CalledNumber = request.Destination,
        VoiceSdkAppid = Required(request.Configuration, "VoiceSdkAppid"),
        TemplateParamSet = ResolveTemplateParameters(request),
        PlayTimes = ulong.TryParse(
            request.Configuration.GetValueOrDefault("PlayTimes"),
            out var playTimes)
            ? playTimes
            : 1,
        SessionContext = request.IdempotencyKey
    };

    private static NotificationChannelResult Failure(
        string? code,
        string? message,
        string? requestId)
    {
        var transient = code?.Contains("Limit", StringComparison.OrdinalIgnoreCase) == true ||
                        code?.Contains("Internal", StringComparison.OrdinalIgnoreCase) == true ||
                        code?.Contains("Timeout", StringComparison.OrdinalIgnoreCase) == true ||
                        code?.Contains("Unavailable", StringComparison.OrdinalIgnoreCase) == true;
        return new(
            transient
                ? NotificationChannelResultStatus.TransientFailure
                : NotificationChannelResultStatus.PermanentFailure,
            requestId,
            code,
            message,
            ConfirmedNotAccepted: true);
    }

    private static string[] ResolveTemplateParameters(NotificationChannelRequest request)
    {
        var order = request.Configuration.GetValueOrDefault("TemplateParameterOrder")
            ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return order is { Length: > 0 }
            ? order.Select(key =>
                    request.TemplateParameters.GetValueOrDefault(key) ?? string.Empty)
                .ToArray()
            : request.TemplateParameters
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => item.Value)
                .ToArray();
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        !string.IsNullOrWhiteSpace(values.GetValueOrDefault(key))
            ? values[key]
            : throw new InvalidOperationException($"Tencent voice setting '{key}' is required.");

    private static string ResolveEndpoint(string? configured, string defaultHost)
    {
        if (string.IsNullOrWhiteSpace(configured)) return defaultHost;
        return Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            ? uri.Host
            : configured;
    }
}

public sealed class AliyunVoiceNotificationProvider : INotificationChannelProvider
{
    public string Channel => NotificationChannels.Voice;
    public string Provider => NotificationProviders.AliyunVoice;
    public ProviderCapabilities Capabilities => new(true, true);

    public async Task<NotificationChannelResult> SendAsync(
        NotificationChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = new Config
            {
                AccessKeyId = Required(request.Configuration, "AccessKeyId"),
                AccessKeySecret = Required(request.Configuration, "AccessKeySecret"),
                Endpoint = ResolveEndpoint(
                    request.Configuration.GetValueOrDefault("Endpoint"),
                    "dyvmsapi.aliyuncs.com"),
                Protocol = "HTTPS"
            };
            var client = new AliyunVoiceClient(config);
            var response = await client.SingleCallByTtsAsync(CreateRequest(request))
                .WaitAsync(cancellationToken);
            if (response.Body?.Code == "OK")
            {
                return NotificationChannelResult.Accepted(
                    response.Body.CallId ?? response.Body.RequestId);
            }

            var code = response.Body?.Code ?? "empty_response";
            var transient = code.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) ||
                            code.Contains("INTERNAL", StringComparison.OrdinalIgnoreCase) ||
                            code.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
                            code.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase);
            return new(
                transient
                    ? NotificationChannelResultStatus.TransientFailure
                    : NotificationChannelResultStatus.PermanentFailure,
                response.Body?.RequestId,
                code,
                response.Body?.Message ?? "Aliyun voice returned no response body.",
                ConfirmedNotAccepted: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(NotificationChannelResultStatus.Unknown, ErrorCode: "timeout");
        }
        catch (TeaException exception)
        {
            return AliyunNotificationFailureMapper.FromService(exception, "aliyun_voice_error");
        }
        catch (Exception exception)
            when (exception is TeaRetryableException or TeaUnretryableException)
        {
            return AliyunNotificationFailureMapper.FromTransport(
                exception,
                "aliyun_voice_transport_error");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(
                NotificationChannelResultStatus.PermanentFailure,
                ErrorCode: "aliyun_voice_error",
                ErrorMessage: exception.Message,
                ConfirmedNotAccepted: true);
        }
    }

    internal static AliyunSingleCallByTtsRequest CreateRequest(
        NotificationChannelRequest request) => new()
    {
        CalledNumber = request.Destination,
        CalledShowNumber = request.Configuration.GetValueOrDefault("CalledShowNumber"),
        TtsCode = request.TemplateCode ?? Required(request.Configuration, "TtsCode"),
        TtsParam = JsonSerializer.Serialize(request.TemplateParameters),
        PlayTimes = int.TryParse(
            request.Configuration.GetValueOrDefault("PlayTimes"),
            out var playTimes)
            ? playTimes
            : 1
    };

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        !string.IsNullOrWhiteSpace(values.GetValueOrDefault(key))
            ? values[key]
            : throw new InvalidOperationException($"Aliyun voice setting '{key}' is required.");

    private static string ResolveEndpoint(string? configured, string defaultHost)
    {
        if (string.IsNullOrWhiteSpace(configured)) return defaultHost;
        return Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            ? uri.Host
            : configured;
    }
}
