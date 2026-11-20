using Sim.DeviceAgent.Contracts;

namespace Sim.DeviceAgent.Runtime;

public interface IEnvelopeSink : IAsyncDisposable
{
    string Id { get; }

    string Type { get; }

    SinkStatus GetStatus();

    Task StartAsync(CancellationToken cancellationToken);

    ValueTask PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken);
}

public sealed record SinkStatus(
    string Id,
    string Type,
    string State,
    long PendingEntries,
    string? LastError);
