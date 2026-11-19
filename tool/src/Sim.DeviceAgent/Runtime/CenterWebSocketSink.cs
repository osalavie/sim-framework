using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Contracts;
using Sim.DeviceAgent.Spool;

namespace Sim.DeviceAgent.Runtime;

public sealed class CenterWebSocketSink : IEnvelopeSink
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);
    private const int MaxIncomingBytes = 1024 * 1024;

    private readonly string _id;
    private readonly SinkConfig _config;
    private readonly AgentOptions _agent;
    private readonly SpoolStore _spool;
    private readonly ILogger<CenterWebSocketSink> _logger;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private ClientWebSocket? _socket;
    private int _started;
    private int _stopped;
    private int _disposed;
    private int _connected;
    private int _healthy;
    private int _backoffSeconds;
    private long _sentCount;
    private long _ackCount;
    private string? _lastError;

    public CenterWebSocketSink(
        string id,
        SinkConfig config,
        AgentOptions agent,
        ILoggerFactory loggerFactory)
    {
        _id = id;
        _config = config;
        _agent = agent;
        _logger = loggerFactory.CreateLogger<CenterWebSocketSink>();
        _backoffSeconds = config.ReconnectMinSeconds;

        var spoolRoot = string.IsNullOrWhiteSpace(config.SpoolDirectory)
            ? Path.Combine(agent.StateDirectory, "spool", agent.Id, id)
            : config.SpoolDirectory;
        _spool = new SpoolStore(
            Path.GetFullPath(spoolRoot),
            config.SpoolSegmentMegabytes * 1024L * 1024L,
            config.SpoolMaxMegabytes * 1024L * 1024L,
            loggerFactory);
    }

    public string Id => _id;

    public string Type => "wsClient";

    public SinkStatus GetStatus()
    {
        var state = Volatile.Read(ref _stopped) != 0
            ? "stopped"
            : Volatile.Read(ref _connected) != 0
                ? "connected"
                : "disconnected";

        return new SinkStatus(
            _id,
            Type,
            state,
            _spool.PendingCount,
            Volatile.Read(ref _lastError));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _runTask = Task.Run(() => RunAsync(_cts.Token), CancellationToken.None);

        _logger.LogInformation(
            "Center sink {SinkId} started for {Url} (spool: {SpoolRoot}).",
            _id,
            _config.Url,
            _spool.RootDirectory);
        return Task.CompletedTask;
    }

    public async ValueTask PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (string.Equals(envelope.Type, MessageTypes.Status, StringComparison.OrdinalIgnoreCase))
        {
            await PublishStatusAsync(envelope, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await _spool.AppendAsync(envelope, cancellationToken).ConfigureAwait(false);
            _wake.Writer.TryWrite(true);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _cts?.Cancel();
        _wake.Writer.TryWrite(true);

        if (_runTask is not null)
        {
            try
            {
                await _runTask.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
                _logger.LogWarning(ex, "Center sink {SinkId} did not stop cleanly.", _id);
            }
        }

        try
        {
            await _spool.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Center sink {SinkId} failed to flush spool state.", _id);
        }

        _cts?.Dispose();
        _cts = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _spool.DisposeAsync().ConfigureAwait(false);
        _sendLock.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Interlocked.Exchange(ref _healthy, 0);

            try
            {
                await ConnectAndRunAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                SetError(ex.Message);
                _logger.LogWarning(ex, "Center sink {SinkId} connection ended.", _id);
            }
            finally
            {
                Interlocked.Exchange(ref _connected, 0);
                Volatile.Write(ref _socket, null);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await _spool.RewindAllAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (Volatile.Read(ref _healthy) != 0)
            {
                _backoffSeconds = _config.ReconnectMinSeconds;
            }

            var delay = TimeSpan.FromSeconds(_backoffSeconds)
                        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
            _backoffSeconds = Math.Min(_backoffSeconds * 2, _config.ReconnectMaxSeconds);

            _logger.LogInformation(
                "Center sink {SinkId} reconnecting in {DelaySeconds:0.0}s.",
                _id,
                delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ConnectAndRunAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        if (!string.IsNullOrWhiteSpace(_config.Token))
        {
            socket.Options.SetRequestHeader("Authorization", $"Bearer {_config.Token}");
        }

        _logger.LogInformation("Center sink {SinkId} connecting to {Url}.", _id, _config.Url);
        await socket.ConnectAsync(new Uri(_config.Url), cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _socket, socket);

        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = ReceiveLoopAsync(socket, connectionCts.Token);
        var pumpTask = PumpLoopAsync(socket, connectionCts.Token);
        var heartbeatTask = HeartbeatLoopAsync(socket, connectionCts.Token);

        try
        {
            await SendHelloAsync(socket, cancellationToken).ConfigureAwait(false);
            Interlocked.Exchange(ref _connected, 1);
            SetError(null);
            _wake.Writer.TryWrite(true);

            _logger.LogInformation("Center sink {SinkId} connected.", _id);

            var completed = await Task.WhenAny(receiveTask, pumpTask, heartbeatTask).ConfigureAwait(false);
            if (completed.IsFaulted)
            {
                await completed.ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _connected, 0);
            connectionCts.Cancel();
            socket.Abort();

            try
            {
                await Task.WhenAll(receiveTask, pumpTask, heartbeatTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutdown or reconnect path.
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Center sink {SinkId} background loop stopped.", _id);
            }

            Volatile.Write(ref _socket, null);
        }
    }

    private async Task PumpLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _wake.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested)
            {
                var entry = await _spool.TryReadNextAsync(cancellationToken).ConfigureAwait(false);
                if (entry is null)
                {
                    break;
                }

                await SendTextAsync(socket, entry.Json, cancellationToken).ConfigureAwait(false);
                Interlocked.Increment(ref _sentCount);
            }
        }
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(HeartbeatInterval, cancellationToken).ConfigureAwait(false);

            var heartbeat = JsonSerializer.Serialize(new
            {
                v = 1,
                type = "heartbeat",
                agentId = _agent.Id,
                ts = DateTimeOffset.UtcNow,
                pending = _spool.PendingCount,
                sent = Interlocked.Read(ref _sentCount),
                acked = Interlocked.Read(ref _ackCount)
            }, AgentJson.Options);

            await SendTextAsync(socket, heartbeat, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException) when (socket.State != WebSocketState.Open)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation(
                    "Center sink {SinkId} received close frame ({Status}: {Description}).",
                    _id,
                    result.CloseStatus,
                    result.CloseStatusDescription);
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                if (result.EndOfMessage)
                {
                    message.SetLength(0);
                }

                continue;
            }

            message.Write(buffer, 0, result.Count);
            if (message.Length > MaxIncomingBytes)
            {
                throw new InvalidOperationException("Center sent a message larger than the 1 MiB limit.");
            }

            if (!result.EndOfMessage)
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);
            HandleCenterMessage(text);
        }
    }

    private void HandleCenterMessage(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

            switch (type)
            {
                case MessageTypes.Ack:
                    HandleAck(root);
                    break;

                case MessageTypes.HelloAck:
                    Interlocked.Exchange(ref _healthy, 1);
                    _logger.LogInformation("Center sink {SinkId} handshake accepted.", _id);
                    break;

                case MessageTypes.Error:
                    var message = root.TryGetProperty("error", out var error) &&
                                  error.ValueKind == JsonValueKind.Object &&
                                  error.TryGetProperty("message", out var errorMessage)
                        ? errorMessage.GetString()
                        : text;
                    SetError(message);
                    _logger.LogWarning("Center sink {SinkId} reported an error: {Message}", _id, message);
                    break;

                default:
                    _logger.LogDebug("Center sink {SinkId} ignored message type {Type}.", _id, type);
                    break;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Center sink {SinkId} received malformed JSON.", _id);
        }
    }

    private void HandleAck(JsonElement root)
    {
        if (!root.TryGetProperty("deviceId", out var deviceElement) ||
            deviceElement.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("seq", out var seqElement) ||
            !seqElement.TryGetInt64(out var seq) ||
            seq <= 0)
        {
            _logger.LogWarning("Center sink {SinkId} received an invalid ack.", _id);
            return;
        }

        if (root.TryGetProperty("agentId", out var agentElement) &&
            (agentElement.ValueKind != JsonValueKind.String ||
             !string.Equals(agentElement.GetString(), _agent.Id, StringComparison.Ordinal)))
        {
            _logger.LogWarning("Center sink {SinkId} received an ack for another agent.", _id);
            return;
        }

        var deviceId = deviceElement.GetString();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        _spool.MarkDelivered(deviceId, seq);
        Interlocked.Increment(ref _ackCount);
        Interlocked.Exchange(ref _healthy, 1);
        SetError(null);
    }

    private async ValueTask PublishStatusAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        var socket = Volatile.Read(ref _socket);
        if (socket is not { State: WebSocketState.Open })
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(envelope, AgentJson.Options);
            await SendTextAsync(socket, json, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            TryAbort(socket);
        }
    }

    private async ValueTask SendHelloAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var hello = JsonSerializer.Serialize(new
        {
            v = 1,
            type = MessageTypes.Hello,
            agentId = _agent.Id,
            displayName = _agent.DisplayName,
            ts = DateTimeOffset.UtcNow
        }, AgentJson.Options);

        await SendTextAsync(socket, hello, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SendTextAsync(
        ClientWebSocket socket,
        string text,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket.State != WebSocketState.Open)
            {
                throw new WebSocketException("The center WebSocket is not open.");
            }

            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void SetError(string? message) => Volatile.Write(ref _lastError, message);

    private static void TryAbort(ClientWebSocket socket)
    {
        try
        {
            socket.Abort();
        }
        catch
        {
            // Best effort: the connection loop will replace the socket.
        }
    }
}
