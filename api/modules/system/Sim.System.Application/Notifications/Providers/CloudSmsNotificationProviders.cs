using System.Text.Json;
using AlibabaCloud.OpenApiClient.Models;
using AlibabaCloud.SDK.Dysmsapi20170525.Models;
using Sim.System.Notifications;
using TencentCloud.Common;
using TencentCloud.Common.Profile;
using TencentCloud.Sms.V20210111;
using TencentCloud.Sms.V20210111.Models;
using Tea;
using AliyunSmsClient = AlibabaCloud.SDK.Dysmsapi20170525.Client;
using AliyunSendSmsRequest = AlibabaCloud.SDK.Dysmsapi20170525.Models.SendSmsRequest;
using TencentSendSmsRequest = TencentCloud.Sms.V20210111.Models.SendSmsRequest;

namespace Sim.System.Application.Notifications.Providers;

public sealed class TencentSmsNotificationProvider : INotificationChannelProvider
{
    public string Channel => NotificationChannels.Sms;
    public string Provider => NotificationProviders.TencentSms;
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
            var endpoint = ResolveEndpoint(
                request.Configuration.GetValueOrDefault("Endpoint"),
                "sms.tencentcloudapi.com");
            var profile = new ClientProfile
            {
                HttpProfile = new HttpProfile
                {
                    Endpoint = endpoint,
                    Timeout = 15
                }
            };
            var client = new SmsClient(credential, region, profile);
            var response = await client.SendSms(CreateRequest(request)).WaitAsync(cancellationToken);
            var status = response.SendStatusSet?.FirstOrDefault();
            if (status?.Code == "Ok")
            {
                return NotificationChannelResult.Accepted(status.SerialNo ?? response.RequestId);
            }

            return Failure(
                status?.Code ?? "empty_response",
                status?.Message ?? "Tencent SMS returned no send status.",
                response.RequestId);
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
                ErrorCode: "tencent_sms_configuration_error",
                ErrorMessage: exception.Message,
                ConfirmedNotAccepted: true);
        }
    }

    internal static TencentSendSmsRequest CreateRequest(NotificationChannelRequest request) => new()
    {
        PhoneNumberSet = [request.Destination],
        SmsSdkAppId = Required(request.Configuration, "SmsSdkAppId"),
        SignName = Required(request.Configuration, "SignName"),
        TemplateId = request.TemplateCode ?? Required(request.Configuration, "TemplateId"),
        TemplateParamSet = ResolveTemplateParameters(request),
        SessionContext = request.IdempotencyKey
    };

    private static NotificationChannelResult Failure(
        string? code,
        string? message,
        string? requestId = null)
    {
        var transient = code?.Contains("Limit", StringComparison.OrdinalIgnoreCase) == true ||
                        code?.Contains("Internal", StringComparison.OrdinalIgnoreCase) == true ||
                        code?.Contains("Unavailable", StringComparison.OrdinalIgnoreCase) == true ||
                        code?.Contains("Timeout", StringComparison.OrdinalIgnoreCase) == true;
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
            : throw new InvalidOperationException($"Tencent SMS setting '{key}' is required.");

    private static string ResolveEndpoint(string? configured, string defaultHost)
    {
        if (string.IsNullOrWhiteSpace(configured)) return defaultHost;
        return Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            ? uri.Host
            : configured;
    }
}

public sealed class AliyunSmsNotificationProvider : INotificationChannelProvider
{
    public string Channel => NotificationChannels.Sms;
    public string Provider => NotificationProviders.AliyunSms;
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
                    "dysmsapi.aliyuncs.com"),
                Protocol = "HTTPS"
            };
            var client = new AliyunSmsClient(config);
            var response = await client.SendSmsAsync(CreateRequest(request))
                .WaitAsync(cancellationToken);
            if (response.Body?.Code == "OK")
            {
                return NotificationChannelResult.Accepted(
                    response.Body.BizId ?? response.Body.RequestId);
            }

            var code = response.Body?.Code ?? "empty_response";
            var transient =
                code.Contains("BUSINESS_LIMIT_CONTROL", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("INTERNAL", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("TIMEOUT", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("UNAVAILABLE", StringComparison.OrdinalIgnoreCase);
            return new(
                transient
                    ? NotificationChannelResultStatus.TransientFailure
                    : NotificationChannelResultStatus.PermanentFailure,
                response.Body?.RequestId,
                code,
                response.Body?.Message ?? "Aliyun SMS returned no response body.",
                ConfirmedNotAccepted: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(NotificationChannelResultStatus.Unknown, ErrorCode: "timeout");
        }
        catch (TeaException exception)
        {
            return AliyunNotificationFailureMapper.FromService(exception, "aliyun_sms_error");
        }
        catch (Exception exception)
            when (exception is TeaRetryableException or TeaUnretryableException)
        {
            return AliyunNotificationFailureMapper.FromTransport(
                exception,
                "aliyun_sms_transport_error");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(
                NotificationChannelResultStatus.PermanentFailure,
                ErrorCode: "aliyun_sms_error",
                ErrorMessage: exception.Message,
                ConfirmedNotAccepted: true);
        }
    }

    internal static AliyunSendSmsRequest CreateRequest(NotificationChannelRequest request) => new()
    {
        PhoneNumbers = request.Destination,
        SignName = Required(request.Configuration, "SignName"),
        TemplateCode = request.TemplateCode ?? Required(request.Configuration, "TemplateCode"),
        TemplateParam = JsonSerializer.Serialize(request.TemplateParameters)
    };

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        !string.IsNullOrWhiteSpace(values.GetValueOrDefault(key))
            ? values[key]
            : throw new InvalidOperationException($"Aliyun SMS setting '{key}' is required.");

    private static string ResolveEndpoint(string? configured, string defaultHost)
    {
        if (string.IsNullOrWhiteSpace(configured)) return defaultHost;
        return Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            ? uri.Host
            : configured;
    }
}
