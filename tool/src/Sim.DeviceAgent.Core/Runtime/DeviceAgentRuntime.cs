using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sim.DeviceAgent.Codecs;
using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Sources;
using Sim.DeviceAgent.WebSockets;

namespace Sim.DeviceAgent.Runtime;

public sealed class DeviceAgentRuntime : IHostedService, IAsyncDisposable
{
    private readonly AgentConfig _config;
    private readonly AgentChannelRegistry _registry;
    private readonly AgentSinkRegistry _sinkRegistry;
    private readonly LocalWebSocketHub _localHub;
    private readonly ISequenceStore _sequenceStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<DeviceAgentRuntime> _logger;
    private readonly List<DeviceChannel> _channels = [];
    private readonly List<IEnvelopeSink> _sinks = [];
    private readonly Dictionary<string, IEnvelopeSink> _sinksById = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private int _started;
    private int _running;
    private int _stopped;
    private int _disposed;

    public DeviceAgentRuntime(
        AgentConfig config,
        AgentChannelRegistry registry,
        AgentSinkRegistry sinkRegistry,
        LocalWebSocketHub localHub,
        ISequenceStore sequenceStore,
        ILoggerFactory loggerFactory,
        ILogger<DeviceAgentRuntime> logger)
    {
        _config = config;
        _registry = registry;
        _sinkRegistry = sinkRegistry;
        _localHub = localHub;
        _sequenceStore = sequenceStore;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    public bool IsRunning =>
        Volatile.Read(ref _running) != 0 && Volatile.Read(ref _stopped) == 0;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _stopped) != 0)
            {
                return;
            }

            await StartSinksAsync(cancellationToken).ConfigureAwait(false);
            await StartChannelsAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _running, 1);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task ReloadAsync(AgentConfig candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("The agent runtime is not running; configuration cannot be reloaded.");
            }

            _logger.LogInformation("Applying a configuration reload.");

            await StopChannelsAsync(cancellationToken).ConfigureAwait(false);
            await StopSinksAsync(cancellationToken).ConfigureAwait(false);

            _config.Agent = candidate.Agent;
            _config.Sources = candidate.Sources;
            _config.Sinks = candidate.Sinks;
            _config.Channels = candidate.Channels;

            await StartSinksAsync(cancellationToken).ConfigureAwait(false);
            await StartChannelsAsync(cancellationToken).ConfigureAwait(false);
            await _localHub.PublishStatusSnapshotAsync().ConfigureAwait(false);

            _logger.LogInformation(
                "Configuration reload complete: {ChannelCount} channel(s), {SinkCount} sink(s).",
                _channels.Count,
                _sinks.Count);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        await _lifecycleGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            Volatile.Write(ref _running, 0);
            await StopChannelsAsync(cancellationToken).ConfigureAwait(false);
            await StopSinksAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);

        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _lifecycleGate.Dispose();
        }
    }

    private async Task StartSinksAsync(CancellationToken cancellationToken)
    {
        _sinkRegistry.Clear();

        foreach (var (sinkId, sinkConfig) in _config.Sinks)
        {
            IEnvelopeSink? sink = null;
            try
            {
                sink = CreateSink(sinkId, sinkConfig);
                await sink.StartAsync(cancellationToken).ConfigureAwait(false);
                _sinks.Add(sink);
                _sinksById[sinkId] = sink;
                _sinkRegistry.Add(sink);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sink {SinkId} failed to start.", sinkId);
                if (sink is not null)
                {
                    try
                    {
                        await sink.DisposeAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // The original startup failure is the actionable error.
                    }
                }
            }
        }
    }

    private async Task StartChannelsAsync(CancellationToken cancellationToken)
    {
        var sources = _config.Sources.ToDictionary(
            source => source.Id,
            source => source,
            StringComparer.OrdinalIgnoreCase);

        _registry.Clear();

        foreach (var channelConfig in _config.Channels.Where(channel => channel.Enabled))
        {
            if (!sources.TryGetValue(channelConfig.SourceRef, out var sourceConfig))
            {
                _logger.LogError(
                    "Channel {ChannelId} references unknown source {SourceRef}; the channel is skipped.",
                    channelConfig.Id,
                    channelConfig.SourceRef);
                continue;
            }

            var channelSinks = ResolveSinks(channelConfig);
            if (channelSinks.Count == 0)
            {
                _logger.LogError(
                    "Channel {ChannelId} has no available sink; the channel is skipped.",
                    channelConfig.Id);
                continue;
            }

            IDeviceSource? source = null;
            try
            {
                source = SourceFactory.Create(sourceConfig, _loggerFactory);
                var codec = CodecFactory.Create(channelConfig.Codec, sourceConfig.Type);
                var channel = new DeviceChannel(
                    _config.Agent.Id,
                    channelConfig,
                    source,
                    codec,
                    channelSinks,
                    _sequenceStore,
                    _loggerFactory.CreateLogger<DeviceChannel>());

                _channels.Add(channel);
                _registry.Add(channel);
                await channel.StartAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Started device channel {ChannelId} (device={DeviceId}, type={DeviceType}, source={SourceId}, sinks={Sinks}).",
                    channelConfig.Id,
                    channelConfig.DeviceId,
                    channelConfig.DeviceType,
                    sourceConfig.Id,
                    string.Join(",", channelSinks.Select(sink => sink.Id)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Channel {ChannelId} failed to start.", channelConfig.Id);
                if (source is not null)
                {
                    await source.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    private async Task StopChannelsAsync(CancellationToken cancellationToken)
    {
        if (_channels.Count > 0)
        {
            try
            {
                await Task.WhenAll(_channels.Select(channel => channel.StopAsync()))
                    .WaitAsync(TimeSpan.FromSeconds(15), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "One or more device channels did not stop cleanly.");
            }
        }

        _channels.Clear();
        _registry.Clear();
    }

    private async Task StopSinksAsync(CancellationToken cancellationToken)
    {
        for (var index = _sinks.Count - 1; index >= 0; index--)
        {
            var sink = _sinks[index];
            try
            {
                await sink.StopAsync(cancellationToken).ConfigureAwait(false);
                await sink.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sink {SinkId} did not stop cleanly.", sink.Id);
            }
        }

        _sinks.Clear();
        _sinksById.Clear();
        _sinkRegistry.Clear();
    }

    private IEnvelopeSink CreateSink(string sinkId, SinkConfig sinkConfig)
    {
        return sinkConfig.Type.ToLowerInvariant() switch
        {
            "wsserver" => new LocalHubSink(sinkId, _localHub),
            "wsclient" => new CenterWebSocketSink(
                sinkId,
                sinkConfig,
                _config.Agent,
                _loggerFactory),
            _ => throw new InvalidOperationException($"Unsupported sink type '{sinkConfig.Type}'.")
        };
    }

    private IReadOnlyList<IEnvelopeSink> ResolveSinks(ChannelConfig channelConfig)
    {
        var sinks = new List<IEnvelopeSink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sinkRef in channelConfig.Sinks)
        {
            if (!seen.Add(sinkRef))
            {
                continue;
            }

            if (_sinksById.TryGetValue(sinkRef, out var sink))
            {
                sinks.Add(sink);
            }
            else
            {
                _logger.LogWarning(
                    "Channel {ChannelId} references sink {SinkId}, but that sink is not running.",
                    channelConfig.Id,
                    sinkRef);
            }
        }

        return sinks;
    }
}
