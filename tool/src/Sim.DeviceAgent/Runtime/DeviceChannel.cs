using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Sim.DeviceAgent.Codecs;
using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Contracts;
using Sim.DeviceAgent.Sources;

namespace Sim.DeviceAgent.Runtime;

public sealed class DeviceChannel : IAsyncDisposable
{
    private readonly string _agentId;
    private readonly ChannelConfig _config;
    private readonly IDeviceSource _source;
    private readonly IDeviceCodec _codec;
    private readonly IReadOnlyList<IEnvelopeSink> _sinks;
    private readonly ISequenceStore _sequenceStore;
    private readonly ILogger<DeviceChannel> _logger;
    private readonly Encoding _encoding;
    private readonly Channel<QueuedFrame> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _statusLock = new();

    private IFrameDecoder? _decoder;
    private Task? _sourceTask;
    private Task? _processTask;
    private PendingCommand? _pendingCommand;
    private int _commandInFlight;
    private int _stopped;
    private long _seq;
    private string _state = SourceState.Connecting.ToString().ToLowerInvariant();
    private DateTimeOffset? _lastDataAt;
    private long _receivedFrames;
    private long _droppedFrames;
    private long _parseErrors;
    private string? _lastError;

    public DeviceChannel(
        string agentId,
        ChannelConfig config,
        IDeviceSource source,
        IDeviceCodec codec,
        IReadOnlyList<IEnvelopeSink> sinks,
        ISequenceStore sequenceStore,
        ILogger<DeviceChannel> logger)
    {
        _agentId = agentId;
        _config = config;
        _source = source;
        _codec = codec;
        _sinks = sinks;
        _sequenceStore = sequenceStore;
        _logger = logger;
        _encoding = EncodingResolver.Resolve(config.Codec.Encoding);
        _queue = Channel.CreateBounded<QueuedFrame>(new BoundedChannelOptions(config.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    public string ChannelId => _config.Id;

    public string DeviceId => _config.DeviceId;

    public string DeviceType => _config.DeviceType;

    public string SourceId => _source.Id;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        UpdateState(SourceState.Connecting, null);
        await PublishStatusAsync(cancellationToken);

        var token = _cts.Token;
        _sourceTask = Task.Run(() => _source.RunAsync(HandleSourceEventAsync, token), token);
        _processTask = Task.Run(() => ProcessLoopAsync(token), token);
    }

    public async Task<CommandOutcome> SendCommandAsync(
        string text,
        TimeSpan timeout,
        string? matchPattern,
        CancellationToken cancellationToken)
    {
        if (!_source.CanWrite)
        {
            return CommandOutcome.Fail("write_not_supported", $"Channel '{ChannelId}' uses a read-only source.");
        }

        if (Interlocked.CompareExchange(ref _commandInFlight, 1, 0) != 0)
        {
            return CommandOutcome.Fail("command_busy", "Another command is already waiting for a response on this device.");
        }

        try
        {
            Regex? matcher = null;
            if (!string.IsNullOrWhiteSpace(matchPattern))
            {
                try
                {
                    matcher = new Regex(
                        matchPattern,
                        RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(250));
                }
                catch (ArgumentException ex)
                {
                    return CommandOutcome.Fail("invalid_match_pattern", ex.Message);
                }
            }

            var completion = new TaskCompletionSource<CommandMatch>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = new PendingCommand(matcher, completion);
            Volatile.Write(ref _pendingCommand, pending);

            try
            {
                await _source.WriteAsync(_encoding.GetBytes(text), cancellationToken);
                var effectiveTimeout = timeout <= TimeSpan.Zero
                    ? TimeSpan.FromSeconds(2)
                    : timeout > TimeSpan.FromSeconds(60)
                        ? TimeSpan.FromSeconds(60)
                        : timeout;

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(effectiveTimeout);
                var match = await completion.Task.WaitAsync(timeoutCts.Token);
                return CommandOutcome.Ok(match);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CommandOutcome.Timeout();
            }
            catch (OperationCanceledException)
            {
                return CommandOutcome.Fail("command_cancelled", "The command was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Command on channel {ChannelId} failed.", ChannelId);
                return CommandOutcome.Fail("command_failed", ex.Message);
            }
            finally
            {
                Volatile.Write(ref _pendingCommand, null);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _commandInFlight, 0);
        }
    }

    public ChannelStatus GetStatus()
    {
        lock (_statusLock)
        {
            return new ChannelStatus(
                _config.Id,
                _config.DeviceId,
                _config.DeviceType,
                _config.DeviceName,
                _source.Id,
                _state,
                _lastDataAt,
                Interlocked.Read(ref _seq),
                _receivedFrames,
                _droppedFrames,
                _parseErrors,
                _lastError);
        }
    }

    public async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        Volatile.Read(ref _pendingCommand)?.Completion.TrySetException(new OperationCanceledException("The channel is stopping."));

        try
        {
            await _source.DisposeAsync();
        }
        catch
        {
            // Ignore source disposal failures during shutdown.
        }

        var tasks = new[] { _sourceTask, _processTask }
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();

        if (tasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Channel {ChannelId} did not stop cleanly.", ChannelId);
            }
        }

        _queue.Writer.TryComplete();
        UpdateState(SourceState.Stopped, null);
        await PublishStatusAsync(CancellationToken.None);
        _cts.Dispose();
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task<SourceEventResult> HandleSourceEventAsync(SourceEvent sourceEvent, CancellationToken cancellationToken)
    {
        switch (sourceEvent)
        {
            case SourceBytes bytes:
                return HandleBytes(bytes.Data);

            case SourceFileReady file:
                return await HandleFileAsync(file, cancellationToken);

            case SourceStateChanged state:
                UpdateState(state.State, state.Detail);
                await PublishStatusAsync(cancellationToken);
                return SourceEventResult.Ok;

            default:
                return SourceEventResult.Ok;
        }
    }

    private SourceEventResult HandleBytes(byte[] data)
    {
        try
        {
            _decoder ??= _codec.CreateDecoder();
            foreach (var frame in _decoder.Push(data))
            {
                Enqueue(frame, null);
            }

            return SourceEventResult.Ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Channel {ChannelId} failed to decode a frame.", ChannelId);
            lock (_statusLock)
            {
                _parseErrors++;
            }

            return SourceEventResult.Fail(ex.Message);
        }
    }

    private async Task<SourceEventResult> HandleFileAsync(SourceFileReady file, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                file.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 8192,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var index = 0;
            await foreach (var frame in _codec.DecodeFileAsync(stream, file.Metadata, cancellationToken))
            {
                if (frame.ParseError is not null)
                {
                    lock (_statusLock)
                    {
                        _parseErrors++;
                    }
                }

                Enqueue(
                    frame,
                    new SourceRef(file.Metadata.Name, file.Metadata.Size, file.Metadata.Sha256, index++));
            }

            return SourceEventResult.Ok;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Channel {ChannelId} failed to parse file {File}.", ChannelId, file.Path);
            return SourceEventResult.Fail(ex.Message);
        }
    }

    private async Task ProcessLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(cancellationToken))
            {
                var seq = _sequenceStore.Next(_config.DeviceId);
                Interlocked.Exchange(ref _seq, seq);
                var timestamp = DateTimeOffset.UtcNow;
                lock (_statusLock)
                {
                    _receivedFrames++;
                    _lastDataAt = timestamp;
                }

                var envelope = BuildDataEnvelope(item, seq, timestamp);
                try
                {
                    await PublishAsync(envelope, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                CompletePendingCommand(item.Frame, seq, timestamp);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown path.
        }
    }

    private void Enqueue(DecodedFrame frame, SourceRef? sourceRef)
    {
        var item = new QueuedFrame(frame, sourceRef);
        if (_queue.Writer.TryWrite(item))
        {
            return;
        }

        while (_queue.Reader.TryRead(out _))
        {
            lock (_statusLock)
            {
                _droppedFrames++;
            }

            if (_queue.Writer.TryWrite(item))
            {
                return;
            }
        }

        lock (_statusLock)
        {
            _droppedFrames++;
        }
    }

    private MessageEnvelope BuildDataEnvelope(QueuedFrame item, long seq, DateTimeOffset timestamp)
    {
        var envelope = new MessageEnvelope
        {
            Type = MessageTypes.Data,
            AgentId = _agentId,
            DeviceId = _config.DeviceId,
            DeviceType = _config.DeviceType,
            ChannelId = _config.Id,
            Seq = seq,
            Ts = timestamp,
            Raw = Convert.ToBase64String(item.Frame.Raw),
            Text = item.Frame.Text,
            Parsed = item.Frame.Parsed,
            SourceRef = item.SourceRef
        };

        if (item.Frame.ParseError is not null)
        {
            envelope.Error = new MessageError("parse_error", item.Frame.ParseError);
        }

        return envelope;
    }

    private void CompletePendingCommand(DecodedFrame frame, long seq, DateTimeOffset timestamp)
    {
        var pending = Volatile.Read(ref _pendingCommand);
        if (pending is null)
        {
            return;
        }

        if (pending.Matcher is not null)
        {
            if (frame.Text is null)
            {
                return;
            }

            try
            {
                if (!pending.Matcher.IsMatch(frame.Text))
                {
                    return;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return;
            }
        }

        pending.Completion.TrySetResult(new CommandMatch(frame, seq, timestamp));
    }

    private async Task PublishStatusAsync(CancellationToken cancellationToken)
    {
        var status = GetStatus();
        var envelope = new MessageEnvelope
        {
            Type = MessageTypes.Status,
            AgentId = _agentId,
            DeviceId = status.DeviceId,
            DeviceType = status.DeviceType,
            ChannelId = status.ChannelId,
            Seq = status.Seq,
            Ts = DateTimeOffset.UtcNow,
            State = status.State,
            Payload = JsonSerializer.SerializeToElement(new
            {
                status.SourceId,
                status.LastDataAt,
                status.ReceivedFrames,
                status.DroppedFrames,
                status.ParseErrors
            }, AgentJson.Options)
        };

        if (status.LastError is not null)
        {
            envelope.Error = new MessageError("channel_error", status.LastError);
        }

        try
        {
            await PublishAsync(envelope, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown path.
        }
    }

    private async Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        foreach (var sink in _sinks)
        {
            try
            {
                await sink.PublishAsync(envelope, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sink {SinkId} failed to publish channel {ChannelId}.", sink.Id, ChannelId);
            }
        }
    }

    private void UpdateState(SourceState state, string? detail)
    {
        lock (_statusLock)
        {
            _state = state.ToString().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(detail))
            {
                _lastError = detail;
            }
            else if (state is SourceState.Connected)
            {
                _lastError = null;
            }
        }
    }

    private sealed record QueuedFrame(DecodedFrame Frame, SourceRef? SourceRef);

    private sealed record PendingCommand(Regex? Matcher, TaskCompletionSource<CommandMatch> Completion);
}
