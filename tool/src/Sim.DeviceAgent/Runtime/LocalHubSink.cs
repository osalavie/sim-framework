using Sim.DeviceAgent.Contracts;
using Sim.DeviceAgent.WebSockets;

namespace Sim.DeviceAgent.Runtime;

public sealed class LocalHubSink : IEnvelopeSink
{
    private readonly LocalWebSocketHub _hub;

    public LocalHubSink(string id, LocalWebSocketHub hub)
    {
        Id = id;
        _hub = hub;
    }

    public string Id { get; }

    public string Type => "wsServer";

    public SinkStatus GetStatus() => new(Id, Type, "connected", 0, null);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
        => _hub.PublishAsync(envelope, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
