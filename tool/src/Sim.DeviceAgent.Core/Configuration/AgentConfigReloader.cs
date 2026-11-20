using Microsoft.Extensions.Logging;
using Sim.DeviceAgent.Runtime;

namespace Sim.DeviceAgent.Configuration;

public sealed record ConfigReloadResult(bool Success, string Message, DateTimeOffset At);

public sealed record ConfigReloadStatus(
    string Path,
    DateTimeOffset? LastSuccessAt,
    string? LastError,
    string LastMessage);

public sealed class AgentConfigReloader
{
    private readonly AgentConfigFile _file;
    private readonly AgentConfig _config;
    private readonly DeviceAgentRuntime _runtime;
    private readonly ILogger<AgentConfigReloader> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _statusLock = new();

    private DateTimeOffset? _lastSuccessAt;
    private string? _lastError;
    private string _lastMessage = "No reload has run.";

    public AgentConfigReloader(
        AgentConfigFile file,
        AgentConfig config,
        DeviceAgentRuntime runtime,
        ILogger<AgentConfigReloader> logger)
    {
        _file = file;
        _config = config;
        _runtime = runtime;
        _logger = logger;
    }

    public async Task<ConfigReloadResult> ReloadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            AgentConfig candidate;
            try
            {
                candidate = AgentConfigLoader.Load(_file.Path);
            }
            catch (Exception ex)
            {
                return Fail($"Configuration could not be loaded: {ex.Message}");
            }

            var errors = AgentConfigValidator.Validate(candidate);
            if (errors.Count > 0)
            {
                return Fail($"Configuration is invalid: {string.Join("; ", errors)}");
            }

            var immutableChange = DescribeImmutableChange(candidate.Agent);
            if (immutableChange is not null)
            {
                return Fail(immutableChange);
            }

            var trayEnabledBefore = _config.Agent.Tray.Enabled;
            try
            {
                await _runtime.ReloadAsync(candidate, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Fail($"Configuration reload failed: {ex.Message}");
            }

            var message = "Configuration reloaded.";
            if (trayEnabledBefore != _config.Agent.Tray.Enabled)
            {
                message += " agent.tray.enabled takes effect after restarting the agent.";
            }

            var at = DateTimeOffset.UtcNow;
            lock (_statusLock)
            {
                _lastSuccessAt = at;
                _lastError = null;
                _lastMessage = message;
            }

            _logger.LogInformation("{Message}", message);
            return new ConfigReloadResult(true, message, at);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ConfigReloadStatus GetStatus()
    {
        lock (_statusLock)
        {
            return new ConfigReloadStatus(_file.Path, _lastSuccessAt, _lastError, _lastMessage);
        }
    }

    private string? DescribeImmutableChange(AgentOptions candidate)
    {
        var changes = new List<string>();
        if (!string.Equals(
                _config.Agent.Id,
                candidate.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            changes.Add("agent.id");
        }

        if (!string.Equals(
                Path.GetFullPath(_config.Agent.StateDirectory),
                Path.GetFullPath(candidate.StateDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            changes.Add("agent.stateDirectory");
        }

        if (_config.Agent.LocalWs.Enabled != candidate.LocalWs.Enabled)
        {
            changes.Add("agent.localWs.enabled");
        }

        if (!string.Equals(
                _config.Agent.LocalWs.Bind,
                candidate.LocalWs.Bind,
                StringComparison.OrdinalIgnoreCase))
        {
            changes.Add("agent.localWs.bind");
        }

        if (_config.Agent.LocalWs.Port != candidate.LocalWs.Port)
        {
            changes.Add("agent.localWs.port");
        }

        return changes.Count == 0
            ? null
            : $"Changes to {string.Join(", ", changes)} require restarting the agent; reload rejected.";
    }

    private ConfigReloadResult Fail(string message)
    {
        var at = DateTimeOffset.UtcNow;
        lock (_statusLock)
        {
            _lastError = message;
            _lastMessage = message;
        }

        _logger.LogWarning("{Message}", message);
        return new ConfigReloadResult(false, message, at);
    }
}
