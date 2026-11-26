namespace Sim.Framework.Application.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Framework:Messaging";

    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "Local";
    public int OutboxBatchSize { get; set; } = 100;
    public int PollIntervalMilliseconds { get; set; } = 1000;
    public int StartupDelayMilliseconds { get; set; } = 3000;
    public string Topic { get; set; } = "sim.events.v1";
    public string ConsumerGroup { get; set; } = "sim-admin";
    public string? ConnectionString { get; set; }
    public string ClientId { get; set; } = "sim-admin";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? SecurityProtocol { get; set; }
    public string? SaslMechanism { get; set; }
    public int ConsumerMaxAttempts { get; set; } = 5;
    public string? DeadLetterTopic { get; set; }
    public string Exchange { get; set; } = "sim.events";
    public string Queue { get; set; } = "sim.admin.events";
}
