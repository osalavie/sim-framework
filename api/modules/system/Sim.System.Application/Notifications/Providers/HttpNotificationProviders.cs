using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sim.System.Notifications;

namespace Sim.System.Application.Notifications.Providers;

public abstract class HttpNotificationProvider(
    IHttpClientFactory httpClientFactory,
    string channel,
    string provider) : INotificationChannelProvider
{
    public string Channel { get; } = channel;
    public string Provider { get; } = provider;
    public virtual ProviderCapabilities Capabilities => new(SupportsCallbacks: true);

    public async Task<NotificationChannelResult> SendAsync(
        NotificationChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        var endpoint = Value(request.Configuration, "Endpoint");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return new NotificationChannelResult(
                NotificationChannelResultStatus.PermanentFailure,
                ErrorCode: "invalid_endpoint",
                ErrorMessage: "A valid HTTPS endpoint is required.",
                ConfirmedNotAccepted: true);
        }

        var payload = BuildPayload(request);
        var json = JsonSerializer.Serialize(payload);
        using var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);
        var bearer = Value(request.Configuration, "BearerToken");
        if (!string.IsNullOrWhiteSpace(bearer))
        {
            message.Headers.Authorization = new("Bearer", bearer);
        }

        var secret = Value(request.Configuration, "SigningSecret");
        if (!string.IsNullOrWhiteSpace(secret))
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{json}")))
                .ToLowerInvariant();
            message.Headers.TryAddWithoutValidation("X-Sim-Timestamp", timestamp);
            message.Headers.TryAddWithoutValidation("X-Sim-Signature", $"sha256={signature}");
        }

        try
        {
            using var response = await httpClientFactory.CreateClient("NotificationProvider")
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var requestId = response.Headers.TryGetValues("X-Request-Id", out var values)
                ? values.FirstOrDefault()
                : null;
            if (response.IsSuccessStatusCode)
            {
                return Channel is NotificationChannels.Webhook or NotificationChannels.Feishu or
                    NotificationChannels.WeCom or NotificationChannels.DingTalk
                    ? NotificationChannelResult.Delivered(requestId)
                    : NotificationChannelResult.Accepted(requestId);
            }

            var transient = response.StatusCode == HttpStatusCode.TooManyRequests ||
                            (int)response.StatusCode >= 500;
            return new NotificationChannelResult(
                transient
                    ? NotificationChannelResultStatus.TransientFailure
                    : NotificationChannelResultStatus.PermanentFailure,
                requestId,
                $"http_{(int)response.StatusCode}",
                response.ReasonPhrase,
                ConfirmedNotAccepted: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new NotificationChannelResult(
                NotificationChannelResultStatus.Unknown,
                ErrorCode: "timeout",
                ErrorMessage: "The provider request timed out.");
        }
        catch (HttpRequestException exception)
        {
            return new NotificationChannelResult(
                NotificationChannelResultStatus.TransientFailure,
                ErrorCode: "network_error",
                ErrorMessage: exception.Message,
                ConfirmedNotAccepted: true);
        }
    }

    protected virtual object BuildPayload(NotificationChannelRequest request) => new
    {
        id = request.DeliveryId,
        channel = request.Channel,
        to = request.Destination,
        subject = request.Subject,
        content = request.Body,
        templateCode = request.TemplateCode,
        parameters = request.TemplateParameters
    };

    protected static string? Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
}

public sealed class StandardSmsNotificationProvider(IHttpClientFactory factory)
    : HttpNotificationProvider(factory, NotificationChannels.Sms, NotificationProviders.StandardSms);
public sealed class StandardVoiceNotificationProvider(IHttpClientFactory factory)
    : HttpNotificationProvider(factory, NotificationChannels.Voice, NotificationProviders.StandardVoice);
public sealed class GenericWebhookNotificationProvider(IHttpClientFactory factory)
    : HttpNotificationProvider(factory, NotificationChannels.Webhook, NotificationProviders.GenericWebhook);

public abstract class RobotNotificationProvider(
    IHttpClientFactory factory,
    string channel,
    string provider) : HttpNotificationProvider(factory, channel, provider)
{
    public override ProviderCapabilities Capabilities => new();

    protected override object BuildPayload(NotificationChannelRequest request) =>
        Channel == NotificationChannels.Feishu
            ? new { msg_type = "text", content = new { text = request.Body } }
            : new { msgtype = "text", text = new { content = request.Body } };
}

public sealed class FeishuNotificationProvider(IHttpClientFactory factory)
    : RobotNotificationProvider(factory, NotificationChannels.Feishu, NotificationProviders.Feishu);
public sealed class WeComNotificationProvider(IHttpClientFactory factory)
    : RobotNotificationProvider(factory, NotificationChannels.WeCom, NotificationProviders.WeCom);
public sealed class DingTalkNotificationProvider(IHttpClientFactory factory)
    : RobotNotificationProvider(factory, NotificationChannels.DingTalk, NotificationProviders.DingTalk);
