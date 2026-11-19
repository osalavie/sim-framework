namespace Sim.DeviceAgent.Runtime;

public sealed record ChannelStatus(
    string ChannelId,
    string DeviceId,
    string DeviceType,
    string DeviceName,
    string SourceId,
    string State,
    DateTimeOffset? LastDataAt,
    long Seq,
    long ReceivedFrames,
    long DroppedFrames,
    long ParseErrors,
    string? LastError);
