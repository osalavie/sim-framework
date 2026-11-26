using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sim.Framework.Domain.Shared.Messaging;

namespace Sim.Framework.Application.Messaging;

public sealed class MessageDispatcher(
    IServiceScopeFactory scopeFactory,
    MessageHandlerRegistry registry,
    ILogger<MessageDispatcher> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task DispatchAsync(
        MessageEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var registrations = registry.Get(envelope.Type);
        if (registrations.Count == 0)
        {
            logger.LogWarning(
                "No message handler is registered. MessageType={MessageType}, MessageId={MessageId}",
                envelope.Type,
                envelope.MessageId);
            return;
        }

        foreach (var registration in registrations)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var inbox = scope.ServiceProvider.GetRequiredService<IMessageInboxStore>();
            var consumer = registration.HandlerType.FullName ?? registration.HandlerType.Name;
            if (!await inbox.TryBeginAsync(
                    envelope.MessageId,
                    consumer,
                    DateTimeOffset.UtcNow,
                    cancellationToken))
            {
                continue;
            }

            try
            {
                var payload = JsonSerializer.Deserialize(
                    envelope.Payload,
                    registration.PayloadType,
                    SerializerOptions)
                    ?? throw new InvalidOperationException(
                        $"Message payload '{envelope.Type}' could not be deserialized.");
                var handler = scope.ServiceProvider.GetRequiredService(registration.HandlerType);
                var context = new MessageContext(
                    envelope.MessageId,
                    envelope.Type,
                    envelope.Version,
                    envelope.OccurredAtUtc,
                    envelope.CorrelationId,
                    envelope.CausationId,
                    envelope.TenantId,
                    envelope.Headers ?? new Dictionary<string, string>());
                var handlerContract = typeof(IMessageHandler<>).MakeGenericType(registration.PayloadType);
                var method = handlerContract.GetMethod(nameof(IMessageHandler<object>.HandleAsync))
                    ?? throw new InvalidOperationException(
                        $"Handler '{registration.HandlerType.FullName}' does not expose HandleAsync.");
                var task = (Task?)method.Invoke(handler, [payload, context, cancellationToken])
                    ?? throw new InvalidOperationException("Message handler returned no task.");
                await task;
                await inbox.MarkCompletedAsync(
                    envelope.MessageId,
                    consumer,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                var error = exception.GetBaseException().Message;
                await inbox.MarkFailedAsync(
                    envelope.MessageId,
                    consumer,
                    error,
                    cancellationToken);
                logger.LogError(
                    exception,
                    "Message handler failed. MessageType={MessageType}, MessageId={MessageId}, Consumer={Consumer}",
                    envelope.Type,
                    envelope.MessageId,
                    consumer);
                throw;
            }
        }
    }
}
