using System.Text;
using System.Text.Json;
using Sim.DeviceAgent.Contracts;

namespace Sim.DeviceAgent.Spool;

public sealed class DeviceSpool : IAsyncDisposable
{
    private const string SegmentPrefix = "segment-";
    private const string SegmentSuffix = ".jsonl";
    private const string StateFileName = "state.json";
    private const int StatePersistEvery = 100;
    private const long StatePersistIntervalMs = 1000;

    private readonly string _deviceId;
    private readonly string _directory;
    private readonly string _stateFile;
    private readonly long _maxSegmentBytes;
    private readonly long _maxTotalBytes;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _appendLock = new(1, 1);
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private readonly object _stateLock = new();
    private readonly object _segmentsLock = new();
    private readonly Dictionary<string, long> _segmentMaxSeq = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _completedSegments = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _consumedSegments = new(StringComparer.OrdinalIgnoreCase);

    private FileStream? _appendStream;
    private string? _activeSegmentPath;
    private int _activeSegmentIndex;
    private long _totalBytes;
    private long _lastAppendedSeq;
    private long _lastDeliveredSeq;
    private int _deliveredSincePersist;
    private long _lastPersistTicks;

    private StreamReader? _reader;
    private FileStream? _readerStream;
    private string? _readerSegmentPath;

    public DeviceSpool(string deviceId, string directory, long maxSegmentBytes, long maxTotalBytes, ILogger logger)
    {
        _deviceId = deviceId;
        _directory = directory;
        _stateFile = Path.Combine(directory, StateFileName);
        _maxSegmentBytes = maxSegmentBytes;
        _maxTotalBytes = maxTotalBytes;
        _logger = logger;
        _lastPersistTicks = Environment.TickCount64;

        Directory.CreateDirectory(_directory);
        LoadState();
        DiscoverSegments();
    }

    public string DeviceId => _deviceId;

    public long PendingCount => Math.Max(0, Volatile.Read(ref _lastAppendedSeq) - Volatile.Read(ref _lastDeliveredSeq));

    public async ValueTask AppendAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        if (envelope.Seq is not > 0)
        {
            throw new InvalidOperationException($"Spool entries require a positive sequence for device '{_deviceId}'.");
        }

        var seq = envelope.Seq.Value;
        var json = JsonSerializer.Serialize(envelope, AgentJson.Options);
        var buffer = Encoding.UTF8.GetBytes(json + "\n");

        await _appendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Interlocked.Read(ref _totalBytes) + buffer.Length > _maxTotalBytes)
            {
                throw new IOException(
                    $"Spool quota exceeded for device '{_deviceId}' under '{_directory}'. " +
                    $"Pending data must be delivered before more messages can be accepted.");
            }

            if (_appendStream is null)
            {
                if (_activeSegmentIndex <= 0)
                {
                    _activeSegmentIndex = 1;
                }

                OpenAppendStream(_activeSegmentIndex);
            }
            else if (_appendStream.Length + buffer.Length > _maxSegmentBytes && _appendStream.Length > 0)
            {
                await _appendStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await _appendStream.DisposeAsync().ConfigureAwait(false);
                _appendStream = null;
                _activeSegmentIndex++;
                OpenAppendStream(_activeSegmentIndex);
            }

            await _appendStream!.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await _appendStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Add(ref _totalBytes, buffer.Length);
            Interlocked.Exchange(ref _lastAppendedSeq, seq);

            var activePath = Volatile.Read(ref _activeSegmentPath);
            if (activePath is not null)
            {
                lock (_segmentsLock)
                {
                    _completedSegments.Remove(activePath);
                }
            }

            DeleteDeliveredSegments();
        }
        finally
        {
            _appendLock.Release();
        }
    }

    public async ValueTask<SpoolEntry?> TryReadNextAsync(CancellationToken cancellationToken)
    {
        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_reader is null)
                {
                    var segment = FindOldestSegment();
                    if (segment is null)
                    {
                        return null;
                    }

                    OpenReader(segment);
                }

                var line = await _reader!.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    var currentSegment = _readerSegmentPath;
                    if (currentSegment is not null)
                    {
                        lock (_segmentsLock)
                        {
                            _completedSegments.Add(currentSegment);
                        }
                    }

                    if (currentSegment is not null &&
                        string.Equals(currentSegment, Volatile.Read(ref _activeSegmentPath), StringComparison.Ordinal))
                    {
                        return null;
                    }

                    CloseReader();
                    if (currentSegment is not null)
                    {
                        lock (_segmentsLock)
                        {
                            _consumedSegments.Add(currentSegment);
                        }

                        TryDeleteSegmentIfDelivered(currentSegment);
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                MessageEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<MessageEnvelope>(line, AgentJson.Options);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Skipping malformed spool line in {Segment}.", _readerSegmentPath);
                    continue;
                }

                if (envelope?.Seq is not > 0 ||
                    !string.Equals(envelope.DeviceId, _deviceId, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "Skipping spool entry with unexpected device or sequence in {Segment}.",
                        _readerSegmentPath);
                    continue;
                }

                var seq = envelope.Seq.Value;
                if (_readerSegmentPath is not null)
                {
                    MarkSegmentSeq(_readerSegmentPath, seq);
                }

                if (seq <= Volatile.Read(ref _lastDeliveredSeq))
                {
                    continue;
                }

                return new SpoolEntry(_deviceId, seq, line);
            }
        }
        finally
        {
            _readLock.Release();
        }
    }

    public void MarkDelivered(long seq)
    {
        lock (_stateLock)
        {
            if (seq <= _lastDeliveredSeq)
            {
                return;
            }

            _lastDeliveredSeq = seq;
            Volatile.Write(ref _lastDeliveredSeq, seq);
            _deliveredSincePersist++;

            if (_deliveredSincePersist >= StatePersistEvery ||
                Environment.TickCount64 - _lastPersistTicks >= StatePersistIntervalMs)
            {
                PersistStateLocked();
            }
        }

        DeleteDeliveredSegments();
    }

    public async ValueTask RewindAsync(CancellationToken cancellationToken)
    {
        await _readLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CloseReader();
            lock (_segmentsLock)
            {
                _consumedSegments.Clear();
            }
        }
        finally
        {
            _readLock.Release();
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        await _appendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_appendStream is not null)
            {
                await _appendStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                await _appendStream.DisposeAsync().ConfigureAwait(false);
                _appendStream = null;
            }

            lock (_stateLock)
            {
                PersistStateLocked();
            }
        }
        finally
        {
            _appendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _appendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_appendStream is not null)
            {
                await _appendStream.DisposeAsync().ConfigureAwait(false);
                _appendStream = null;
            }

            lock (_stateLock)
            {
                PersistStateLocked();
            }
        }
        finally
        {
            _appendLock.Release();
            _appendLock.Dispose();
        }

        CloseReader();
        _readLock.Dispose();
    }

    private void OpenAppendStream(int index)
    {
        var path = SegmentPath(index);
        _appendStream = new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        Volatile.Write(ref _activeSegmentPath, path);
    }

    private void OpenReader(string path)
    {
        _readerStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        _reader = new StreamReader(
            _readerStream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 64 * 1024,
            leaveOpen: false);
        _readerSegmentPath = path;
    }

    private void CloseReader()
    {
        _reader?.Dispose();
        _reader = null;
        _readerStream = null;
        _readerSegmentPath = null;
    }

    private string? FindOldestSegment()
    {
        string? oldest = null;
        var oldestIndex = int.MaxValue;

        foreach (var path in Directory.EnumerateFiles(_directory, SegmentPrefix + "*" + SegmentSuffix))
        {
            if (!TryParseSegmentIndex(path, out var index))
            {
                continue;
            }

            lock (_segmentsLock)
            {
                if (_consumedSegments.Contains(path))
                {
                    continue;
                }
            }

            if (index < oldestIndex)
            {
                oldestIndex = index;
                oldest = path;
            }
        }

        return oldest;
    }

    private void MarkSegmentSeq(string path, long seq)
    {
        lock (_segmentsLock)
        {
            if (!_segmentMaxSeq.TryGetValue(path, out var current) || seq > current)
            {
                _segmentMaxSeq[path] = seq;
            }
        }
    }

    private void TryDeleteSegmentIfDelivered(string path)
    {
        if (string.Equals(path, Volatile.Read(ref _activeSegmentPath), StringComparison.Ordinal))
        {
            return;
        }

        long maxSeq;
        lock (_segmentsLock)
        {
            if (!_completedSegments.Contains(path) || !_segmentMaxSeq.TryGetValue(path, out maxSeq))
            {
                return;
            }
        }

        if (maxSeq > Volatile.Read(ref _lastDeliveredSeq))
        {
            return;
        }

        DeleteSegment(path);
        lock (_segmentsLock)
        {
            _segmentMaxSeq.Remove(path);
            _completedSegments.Remove(path);
            _consumedSegments.Remove(path);
        }
    }

    private void DeleteDeliveredSegments()
    {
        string[] candidates;
        lock (_segmentsLock)
        {
            var delivered = Volatile.Read(ref _lastDeliveredSeq);
            candidates = _completedSegments
                .Where(path => _segmentMaxSeq.TryGetValue(path, out var maxSeq) && maxSeq <= delivered)
                .ToArray();
        }

        foreach (var path in candidates)
        {
            TryDeleteSegmentIfDelivered(path);
        }
    }

    private void DeleteSegment(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return;
            }

            var length = file.Length;
            file.Delete();
            Interlocked.Add(ref _totalBytes, -length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to delete delivered spool segment {Segment}.", path);
        }
    }

    private void DiscoverSegments()
    {
        foreach (var path in Directory.EnumerateFiles(_directory, SegmentPrefix + "*" + SegmentSuffix))
        {
            if (!TryParseSegmentIndex(path, out var index))
            {
                continue;
            }

            try
            {
                Interlocked.Add(ref _totalBytes, new FileInfo(path).Length);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to inspect spool segment {Segment}.", path);
            }

            if (index > _activeSegmentIndex)
            {
                _activeSegmentIndex = index;
            }
        }

        if (_activeSegmentIndex > 0)
        {
            Volatile.Write(ref _activeSegmentPath, SegmentPath(_activeSegmentIndex));

            for (var index = _activeSegmentIndex;
                 index > 0 && index > _activeSegmentIndex - 10;
                 index--)
            {
                var lastSeq = TryReadLastSequence(SegmentPath(index));
                if (lastSeq > 0)
                {
                    Interlocked.Exchange(ref _lastAppendedSeq, lastSeq);
                    break;
                }
            }
        }
    }

    private long TryReadLastSequence(string path)
    {
        const int maxTailBytes = 8 * 1024 * 1024;
        const int maxLines = 20;

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            if (length == 0)
            {
                return 0;
            }

            var size = (int)Math.Min(length, maxTailBytes);
            var buffer = new byte[size];
            stream.Seek(length - size, SeekOrigin.Begin);
            stream.ReadExactly(buffer);

            var end = size - 1;
            for (var line = 0; line < maxLines && end >= 0; line++)
            {
                while (end >= 0 && buffer[end] is (byte)'\n' or (byte)'\r' or (byte)' ' or (byte)'\t')
                {
                    end--;
                }

                if (end < 0)
                {
                    return 0;
                }

                var start = end;
                while (start >= 0 && buffer[start] != '\n')
                {
                    start--;
                }

                start++;
                if (start == 0 && size < length)
                {
                    return 0;
                }

                var json = Encoding.UTF8.GetString(buffer, start, end - start + 1);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("seq", out var seqElement) &&
                    seqElement.TryGetInt64(out var seq) &&
                    seq > 0)
                {
                    return seq;
                }

                end = start - 2;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to read the last sequence from spool segment {Segment}.", path);
        }

        return 0;
    }

    private void LoadState()
    {
        if (!File.Exists(_stateFile))
        {
            return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<SpoolState>(File.ReadAllText(_stateFile), AgentJson.Options);
            if (state is { LastDeliveredSeq: > 0 })
            {
                _lastDeliveredSeq = state.LastDeliveredSeq;
                Volatile.Write(ref _lastDeliveredSeq, state.LastDeliveredSeq);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Failed to read spool state {StateFile}; replay starts from the oldest retained segment.",
                _stateFile);
        }
    }

    private void PersistStateLocked()
    {
        var temporary = _stateFile + ".tmp";
        var state = new SpoolState
        {
            DeviceId = _deviceId,
            LastDeliveredSeq = _lastDeliveredSeq
        };
        var json = JsonSerializer.Serialize(state, AgentJson.Options);

        try
        {
            File.WriteAllText(temporary, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, _stateFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to persist spool state {StateFile}.", _stateFile);
        }

        _deliveredSincePersist = 0;
        _lastPersistTicks = Environment.TickCount64;
    }

    private string SegmentPath(int index) => Path.Combine(_directory, $"{SegmentPrefix}{index:D8}{SegmentSuffix}");

    private static bool TryParseSegmentIndex(string path, out int index)
    {
        index = 0;
        var name = Path.GetFileName(path);
        if (!name.StartsWith(SegmentPrefix, StringComparison.Ordinal) ||
            !name.EndsWith(SegmentSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var digits = name[SegmentPrefix.Length..^SegmentSuffix.Length];
        return int.TryParse(digits, out index) && index > 0;
    }

    private sealed class SpoolState
    {
        public string? DeviceId { get; set; }

        public long LastDeliveredSeq { get; set; }
    }
}
