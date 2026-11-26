using System.Text.Json;

namespace Sim.Framework.Domain.Shared.Messaging;

public sealed record MessageEnvelope(
    Guid MessageId,
    string Type,
    int Version,
    DateTimeOffset OccurredAtUtc,
    string Payload,
    string? CorrelationId = null,
    string? CausationId = null,
    Guid? TenantId = null,
    string? PartitionKey = null,
    IReadOnlyDictionary<string, string>? Headers = null)
{
    public T DeserializePayload<T>(JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(Payload, options)
        ?? throw new InvalidOperationException($"Message payload '{Type}' could not be deserialized.");
}

public sealed class MessagePublishOptions
{
    public string? Type { get; init; }
    public int Version { get; init; } = 1;
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public Guid? TenantId { get; init; }
    public string? PartitionKey { get; init; }
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

public sealed record MessageOutboxItem(
    Guid Id,
    MessageEnvelope Envelope,
    int Attempts,
    DateTimeOffset NextAttemptAtUtc);

public interface IMessageBus
{
    Task PublishAsync<T>(
        T message,
        MessagePublishOptions? options = null,
        CancellationToken cancellationToken = default);
}

public interface IMessageHandler<in T>
{
    Task HandleAsync(T message, MessageContext context, CancellationToken cancellationToken = default);
}

public sealed record MessageContext(
    Guid MessageId,
    string Type,
    int Version,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    string? CausationId,
    Guid? TenantId,
    IReadOnlyDictionary<string, string> Headers);

public interface IMessageTransport
{
    string Name { get; }

    Task StartAsync(
        Func<MessageEnvelope, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);

    Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IMessageOutboxStore
{
    Task AddAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageOutboxItem>> GetPendingAsync(
        int maxCount,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(Guid id, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid id,
        string error,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default);
}

public interface IMessageInboxStore
{
    Task<bool> TryBeginAsync(
        Guid messageId,
        string consumer,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Guid messageId,
        string consumer,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid messageId,
        string consumer,
        string error,
        CancellationToken cancellationToken = default);
}
