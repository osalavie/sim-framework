using System.Text.Json;
using Sim.Framework.Domain.Shared.Messaging;
using SqlSugar;

namespace Sim.SqlSugar.Core.Messaging;

public sealed class SqlSugarMessageOutboxStore(ISqlSugarClient db) : IMessageOutboxStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task AddAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entity = new MessageOutboxEntity(Guid.NewGuid())
        {
            TenantId = envelope.TenantId,
            MessageId = envelope.MessageId,
            MessageType = envelope.Type,
            MessageVersion = envelope.Version,
            OccurredAtUtc = envelope.OccurredAtUtc.UtcDateTime,
            Payload = envelope.Payload,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CausationId,
            PartitionKey = envelope.PartitionKey,
            HeadersJson = envelope.Headers == null
                ? null
                : JsonSerializer.Serialize(envelope.Headers, SerializerOptions),
            Status = MessagePersistenceStatuses.Pending,
            NextAttemptAtUtc = DateTime.UtcNow
        };
        return db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MessageOutboxItem>> GetPendingAsync(
        int maxCount,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var leaseExpiredAt = now.UtcDateTime;
        var candidates = await db.Queryable<MessageOutboxEntity>()
            .Where(item =>
                (item.Status == MessagePersistenceStatuses.Pending ||
                 item.Status == MessagePersistenceStatuses.Publishing) &&
                item.NextAttemptAtUtc <= leaseExpiredAt)
            .OrderBy(item => item.NextAttemptAtUtc)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
        var claimed = new List<MessageOutboxEntity>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var previousStatus = candidate.Status;
            var affected = await db.Updateable<MessageOutboxEntity>()
                .SetColumns(item => new MessageOutboxEntity
                {
                    Status = MessagePersistenceStatuses.Publishing,
                    LastAttemptAtUtc = now.UtcDateTime,
                    NextAttemptAtUtc = now.UtcDateTime.AddMinutes(1)
                })
                .Where(item => item.Id == candidate.Id &&
                               item.Status == previousStatus &&
                               item.NextAttemptAtUtc <= leaseExpiredAt)
                .ExecuteCommandAsync(cancellationToken);
            if (affected > 0)
            {
                candidate.Status = MessagePersistenceStatuses.Publishing;
                candidate.LastAttemptAtUtc = now.UtcDateTime;
                candidate.NextAttemptAtUtc = now.UtcDateTime.AddMinutes(1);
                claimed.Add(candidate);
            }
        }

        return claimed.Select(Map).ToArray();
    }

    public Task MarkPublishedAsync(
        Guid id,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default) =>
        db.Updateable<MessageOutboxEntity>()
            .SetColumns(item => new MessageOutboxEntity
            {
                Status = MessagePersistenceStatuses.Published,
                PublishedAtUtc = publishedAtUtc.UtcDateTime,
                LastError = null
            })
            .Where(item => item.Id == id)
            .ExecuteCommandAsync(cancellationToken);

    public Task MarkFailedAsync(
        Guid id,
        string error,
        DateTimeOffset nextAttemptAtUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedError = error.Length <= 2048 ? error : error.Substring(0, 2048);
        return db.Updateable<MessageOutboxEntity>()
            .SetColumns(item => new MessageOutboxEntity
            {
                Attempts = item.Attempts + 1,
                Status = MessagePersistenceStatuses.Pending,
                LastError = normalizedError,
                NextAttemptAtUtc = nextAttemptAtUtc.UtcDateTime
            })
            .Where(item => item.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    private static MessageOutboxItem Map(MessageOutboxEntity entity)
    {
        var headers = string.IsNullOrWhiteSpace(entity.HeadersJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(entity.HeadersJson, SerializerOptions);
        return new MessageOutboxItem(
            entity.Id,
            new MessageEnvelope(
                entity.MessageId,
                entity.MessageType,
                entity.MessageVersion,
                new DateTimeOffset(DateTime.SpecifyKind(entity.OccurredAtUtc, DateTimeKind.Utc)),
                entity.Payload,
                entity.CorrelationId,
                entity.CausationId,
                entity.TenantId,
                entity.PartitionKey,
                headers),
            entity.Attempts,
            new DateTimeOffset(DateTime.SpecifyKind(entity.NextAttemptAtUtc, DateTimeKind.Utc)));
    }
}

public sealed class SqlSugarMessageInboxStore(ISqlSugarClient db) : IMessageInboxStore
{
    public async Task<bool> TryBeginAsync(
        Guid messageId,
        string consumer,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.Queryable<MessageInboxEntity>()
            .FirstAsync(item => item.MessageId == messageId && item.Consumer == consumer, cancellationToken);
        if (existing != null)
        {
            var leaseExpired = existing.Status == MessagePersistenceStatuses.Processing &&
                               existing.ReceivedAtUtc <= receivedAtUtc.UtcDateTime.AddMinutes(-5);
            if (existing.Status != MessagePersistenceStatuses.Failed && !leaseExpired)
            {
                return false;
            }

            return await db.Updateable<MessageInboxEntity>()
                .SetColumns(item => new MessageInboxEntity
                {
                    Status = MessagePersistenceStatuses.Processing,
                    LastError = null,
                    ReceivedAtUtc = receivedAtUtc.UtcDateTime
                })
                .Where(item => item.Id == existing.Id &&
                               (item.Status == MessagePersistenceStatuses.Failed ||
                                (item.Status == MessagePersistenceStatuses.Processing &&
                                 item.ReceivedAtUtc <= receivedAtUtc.UtcDateTime.AddMinutes(-5))))
                .ExecuteCommandAsync(cancellationToken) > 0;
        }

        try
        {
            return await db.Insertable(new MessageInboxEntity(Guid.NewGuid())
                {
                    MessageId = messageId,
                    Consumer = consumer,
                    Status = MessagePersistenceStatuses.Processing,
                    ReceivedAtUtc = receivedAtUtc.UtcDateTime
                })
                .ExecuteCommandAsync(cancellationToken) > 0;
        }
        catch (SqlSugarException)
        {
            return false;
        }
    }

    public Task MarkCompletedAsync(
        Guid messageId,
        string consumer,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            messageId,
            consumer,
            MessagePersistenceStatuses.Completed,
            null,
            completedAtUtc.UtcDateTime,
            cancellationToken);

    public Task MarkFailedAsync(
        Guid messageId,
        string consumer,
        string error,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            messageId,
            consumer,
            MessagePersistenceStatuses.Failed,
            error.Length <= 2048 ? error : error[..2048],
            null,
            cancellationToken);

    private Task UpdateAsync(
        Guid messageId,
        string consumer,
        string status,
        string? error,
        DateTime? completedAtUtc,
        CancellationToken cancellationToken) =>
        db.Updateable<MessageInboxEntity>()
            .SetColumns(item => new MessageInboxEntity
            {
                Status = status,
                LastError = error,
                CompletedAtUtc = completedAtUtc
            })
            .Where(item => item.MessageId == messageId && item.Consumer == consumer)
            .ExecuteCommandAsync(cancellationToken);
}
