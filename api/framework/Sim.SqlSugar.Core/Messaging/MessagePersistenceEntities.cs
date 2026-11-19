using Sim.Framework.Domain.Entities;
using Sim.SqlSugar.Core.Attributes;
using SqlSugar;

namespace Sim.SqlSugar.Core.Messaging;

internal static class MessagePersistenceStatuses
{
    public const string Pending = "pending";
    public const string Publishing = "publishing";
    public const string Published = "published";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

[CodeFirst]
[SugarTable("framework_message_outbox")]
[SugarIndex("uidx_framework_message_outbox_message", nameof(MessageId), OrderByType.Asc, true)]
[SugarIndex("idx_framework_message_outbox_pending", nameof(Status), OrderByType.Asc,
    nameof(NextAttemptAtUtc), OrderByType.Asc)]
public sealed class MessageOutboxEntity : Entity
{
    public MessageOutboxEntity()
    {
    }

    public MessageOutboxEntity(Guid id) : base(id)
    {
    }

    public Guid MessageId { get; set; }

    [SugarColumn(Length = 256, IsNullable = false)]
    public string MessageType { get; set; } = string.Empty;

    public int MessageVersion { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string Payload { get; set; } = string.Empty;

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? CorrelationId { get; set; }

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? CausationId { get; set; }

    [SugarColumn(Length = 256, IsNullable = true)]
    public string? PartitionKey { get; set; }

    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? HeadersJson { get; set; }

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; set; } = MessagePersistenceStatuses.Pending;

    public int Attempts { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }

    [SugarColumn(Length = 2048, IsNullable = true)]
    public string? LastError { get; set; }
}

[CodeFirst]
[SugarTable("framework_message_inbox")]
[SugarIndex("uidx_framework_message_inbox_consumer", nameof(MessageId), OrderByType.Asc,
    nameof(Consumer), OrderByType.Asc, true)]
public sealed class MessageInboxEntity : Entity
{
    public MessageInboxEntity()
    {
    }

    public MessageInboxEntity(Guid id) : base(id)
    {
    }

    public Guid MessageId { get; set; }

    [SugarColumn(Length = 512, IsNullable = false)]
    public string Consumer { get; set; } = string.Empty;

    [SugarColumn(Length = 32, IsNullable = false)]
    public string Status { get; set; } = MessagePersistenceStatuses.Processing;

    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    [SugarColumn(Length = 2048, IsNullable = true)]
    public string? LastError { get; set; }
}
