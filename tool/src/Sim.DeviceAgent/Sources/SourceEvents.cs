namespace Sim.DeviceAgent.Sources;

public enum SourceState
{
    Disconnected,
    Connecting,
    Connected,
    Waiting,
    Faulted,
    Stopped
}

public abstract record SourceEvent;

public sealed record SourceBytes(byte[] Data) : SourceEvent;

public sealed record SourceFileReady(string Path, FileMetadata Metadata) : SourceEvent;

public sealed record SourceStateChanged(SourceState State, string? Detail = null) : SourceEvent;

public sealed record SourceEventResult(bool Success, string? Error)
{
    public static readonly SourceEventResult Ok = new(true, null);

    public static SourceEventResult Fail(string error) => new(false, error);
}

public delegate Task<SourceEventResult> SourceEventHandler(
    SourceEvent sourceEvent,
    CancellationToken cancellationToken);

public interface IDeviceSource : IAsyncDisposable
{
    string Id { get; }

    bool CanWrite { get; }

    Task RunAsync(SourceEventHandler handler, CancellationToken cancellationToken);

    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}
