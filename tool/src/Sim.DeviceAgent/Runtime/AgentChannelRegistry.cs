using System.Collections.Concurrent;

namespace Sim.DeviceAgent.Runtime;

public sealed class AgentChannelRegistry
{
    private readonly ConcurrentDictionary<string, DeviceChannel> _channels = new(StringComparer.OrdinalIgnoreCase);

    public void Add(DeviceChannel channel) => _channels[channel.ChannelId] = channel;

    public void Clear() => _channels.Clear();

    public DeviceChannel? Get(string? channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return null;
        }

        return _channels.TryGetValue(channelId, out var channel) ? channel : null;
    }

    public IReadOnlyList<DeviceChannel> All()
        => _channels.Values.OrderBy(channel => channel.ChannelId, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<ChannelStatus> GetStatuses()
        => All().Select(channel => channel.GetStatus()).ToArray();
}
