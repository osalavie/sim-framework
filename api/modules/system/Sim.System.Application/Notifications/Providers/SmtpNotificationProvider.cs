using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Sim.System.Notifications;

namespace Sim.System.Application.Notifications.Providers;

public sealed class SmtpNotificationProvider : INotificationChannelProvider
{
    public string Channel => NotificationChannels.Email;
    public string Provider => NotificationProviders.Smtp;
    public ProviderCapabilities Capabilities => new();

    public async Task<NotificationChannelResult> SendAsync(
        NotificationChannelRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var host = Required(request.Configuration, "Host");
            var port = int.TryParse(Value(request.Configuration, "Port"), out var parsedPort)
                ? parsedPort
                : 587;
            var socketOptions = ResolveSocketOptions(request.Configuration, port);
            var message = new MimeMessage
            {
                Subject = request.Subject ?? string.Empty,
                Body = bool.TryParse(Value(request.Configuration, "IsBodyHtml"), out var html) && html
                    ? new BodyBuilder { HtmlBody = request.Body }.ToMessageBody()
                    : new TextPart("plain") { Text = request.Body }
            };
            message.From.Add(new MailboxAddress(
                Value(request.Configuration, "FromName") ?? string.Empty,
                Required(request.Configuration, "FromAddress")));
            message.To.Add(MailboxAddress.Parse(request.Destination));

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, socketOptions, cancellationToken);
            var userName = Value(request.Configuration, "UserName");
            if (!string.IsNullOrWhiteSpace(userName))
            {
                await client.AuthenticateAsync(
                    userName,
                    Value(request.Configuration, "Password") ?? string.Empty,
                    cancellationToken);
            }

            var providerRequestId = await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return NotificationChannelResult.Accepted(providerRequestId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(NotificationChannelResultStatus.Unknown, ErrorCode: "smtp_timeout");
        }
        catch (SmtpCommandException exception)
        {
            var permanent = (int)exception.StatusCode >= 500;
            return new NotificationChannelResult(
                permanent
                    ? NotificationChannelResultStatus.PermanentFailure
                    : NotificationChannelResultStatus.TransientFailure,
                ErrorCode: exception.StatusCode.ToString(),
                ErrorMessage: exception.Message,
                ConfirmedNotAccepted: true);
        }
        catch (Exception exception) when (
            exception is SmtpProtocolException or IOException or
                global::System.Net.Sockets.SocketException)
        {
            return new NotificationChannelResult(
                NotificationChannelResultStatus.TransientFailure,
                ErrorCode: "smtp_transport_error",
                ErrorMessage: exception.Message,
                ConfirmedNotAccepted: true);
        }
        catch (Exception exception) when (
            exception is MailKit.Security.AuthenticationException or
                FormatException or InvalidOperationException)
        {
            return new NotificationChannelResult(
                NotificationChannelResultStatus.PermanentFailure,
                ErrorCode: "smtp_configuration_error",
                ErrorMessage: exception.Message,
                ConfirmedNotAccepted: true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new NotificationChannelResult(
                NotificationChannelResultStatus.PermanentFailure,
                ErrorCode: "smtp_error",
                ErrorMessage: exception.Message,
                ConfirmedNotAccepted: true);
        }
    }

    private static SecureSocketOptions ResolveSocketOptions(
        IReadOnlyDictionary<string, string> configuration,
        int port)
    {
        if (Enum.TryParse<SecureSocketOptions>(
                Value(configuration, "SocketOptions"),
                true,
                out var explicitOptions))
        {
            return explicitOptions;
        }

        var useSsl = !bool.TryParse(Value(configuration, "UseSsl"), out var ssl) || ssl;
        if (!useSsl) return SecureSocketOptions.None;
        return port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key) =>
        !string.IsNullOrWhiteSpace(Value(values, key))
            ? Value(values, key)!
            : throw new InvalidOperationException($"SMTP setting '{key}' is required.");

    private static string? Value(IReadOnlyDictionary<string, string> values, string key) =>
        values.FirstOrDefault(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
}
