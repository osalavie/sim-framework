using System.Diagnostics;
using System.Text.Json;
using Sim.Framework.Domain.Shared.Messaging;
using Sim.Framework.Domain.Shared.Security;

namespace Sim.Framework.Application.Messaging;

public sealed class OutboxMessageBus(
    IMessageOutboxStore outboxStore,
    ICurrentTenant currentTenant) : IMessageBus
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task PublishAsync<T>(
        T message,
        MessagePublishOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        options ??= new MessagePublishOptions();
        var activity = Activity.Current;
        var messageType = options.Type ?? typeof(T).FullName
            ?? throw new InvalidOperationException("Message type must have a stable name.");
        var envelope = new MessageEnvelope(
            Guid.NewGuid(),
            messageType,
            options.Version,
            DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(message, SerializerOptions),
            options.CorrelationId ?? activity?.TraceId.ToString(),
            options.CausationId,
            options.TenantId ?? currentTenant.Id,
            options.PartitionKey,
            options.Headers);
        return outboxStore.AddAsync(envelope, cancellationToken);
    }
}
