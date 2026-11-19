using System.Collections.Concurrent;

namespace Sim.DeviceAgent.Runtime;

public sealed class AgentSinkRegistry
{
    private readonly ConcurrentDictionary<string, IEnvelopeSink> _sinks = new(StringComparer.OrdinalIgnoreCase);

    public void Add(IEnvelopeSink sink) => _sinks[sink.Id] = sink;

    public void Clear() => _sinks.Clear();

    public IReadOnlyList<SinkStatus> GetStatuses()
        => _sinks.Values
            .OrderBy(sink => sink.Id, StringComparer.OrdinalIgnoreCase)
            .Select(sink => sink.GetStatus())
            .ToArray();
}
