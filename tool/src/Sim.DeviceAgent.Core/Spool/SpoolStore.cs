using System.Collections.Concurrent;
using Sim.DeviceAgent.Contracts;

namespace Sim.DeviceAgent.Spool;

public sealed class SpoolStore : IAsyncDisposable
{
    private readonly string _rootDirectory;
    private readonly long _maxSegmentBytes;
    private readonly long _maxPerDeviceBytes;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, DeviceSpool> _devices = new(StringComparer.Ordinal);
    private int _cursor;

    public SpoolStore(
        string rootDirectory,
        long maxSegmentBytes,
        long maxPerDeviceBytes,
        ILoggerFactory loggerFactory)
    {
        _rootDirectory = rootDirectory;
        _maxSegmentBytes = maxSegmentBytes;
        _maxPerDeviceBytes = maxPerDeviceBytes;
        _loggerFactory = loggerFactory;
        Directory.CreateDirectory(_rootDirectory);
        DiscoverDevices();
    }

    public string RootDirectory => _rootDirectory;

    public long PendingCount => _devices.Values.Sum(device => device.PendingCount);

    public ValueTask AppendAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(envelope.DeviceId))
        {
            throw new InvalidOperationException("Spool entries require a device id.");
        }

        var device = GetOrCreate(envelope.DeviceId);
        return device.AppendAsync(envelope, cancellationToken);
    }

    public async ValueTask<SpoolEntry?> TryReadNextAsync(CancellationToken cancellationToken)
    {
        var devices = _devices.Values.ToArray();
        if (devices.Length == 0)
        {
            return null;
        }

        var start = Interlocked.Increment(ref _cursor);
        for (var offset = 0; offset < devices.Length; offset++)
        {
            var index = (int)(((long)start + offset) % devices.Length);
            var entry = await devices[index].TryReadNextAsync(cancellationToken).ConfigureAwait(false);
            if (entry is not null)
            {
                return entry;
            }
        }

        return null;
    }

    public void MarkDelivered(string deviceId, long seq)
    {
        if (_devices.TryGetValue(deviceId, out var device))
        {
            device.MarkDelivered(seq);
        }
    }

    public void MarkDelivered(SpoolEntry entry) => MarkDelivered(entry.DeviceId, entry.Seq);

    public async ValueTask RewindAllAsync(CancellationToken cancellationToken)
    {
        foreach (var device in _devices.Values)
        {
            await device.RewindAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        foreach (var device in _devices.Values)
        {
            await device.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var device in _devices.Values)
        {
            await device.DisposeAsync().ConfigureAwait(false);
        }

        _devices.Clear();
    }

    private DeviceSpool GetOrCreate(string deviceId)
    {
        return _devices.GetOrAdd(deviceId, CreateDeviceSpool);
    }

    private DeviceSpool CreateDeviceSpool(string deviceId)
    {
        if (!IsSafeDirectoryName(deviceId))
        {
            throw new ArgumentException(
                $"Device id '{deviceId}' cannot be used as a spool directory name. " +
                "Use letters, digits, '.', '_' or '-'.",
                nameof(deviceId));
        }

        var directory = Path.Combine(_rootDirectory, deviceId);
        return new DeviceSpool(
            deviceId,
            directory,
            _maxSegmentBytes,
            _maxPerDeviceBytes,
            _loggerFactory.CreateLogger($"Spool.{deviceId}"));
    }

    private void DiscoverDevices()
    {
        foreach (var directory in Directory.EnumerateDirectories(_rootDirectory))
        {
            var deviceId = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(deviceId) || !IsSafeDirectoryName(deviceId))
            {
                continue;
            }

            _devices.TryAdd(deviceId, CreateDeviceSpool(deviceId));
        }
    }

    private static bool IsSafeDirectoryName(string value)
    {
        return value.Length > 0 &&
               value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               value is not "." and not "..";
    }
}
