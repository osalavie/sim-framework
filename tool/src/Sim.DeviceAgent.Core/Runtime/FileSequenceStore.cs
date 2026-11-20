using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sim.DeviceAgent.Runtime;

public sealed class FileSequenceStore : ISequenceStore
{
    private const long BlockSize = 100_000;

    private readonly string _stateDirectory;
    private readonly string _stateFile;
    private readonly ILogger<FileSequenceStore> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _reservedHighs;
    private readonly Dictionary<string, DeviceSequence> _devices = new(StringComparer.OrdinalIgnoreCase);

    public FileSequenceStore(string stateDirectory, ILogger<FileSequenceStore> logger)
    {
        _stateDirectory = stateDirectory;
        _stateFile = Path.Combine(stateDirectory, "sequences.json");
        _logger = logger;
        _reservedHighs = LoadState();
    }

    public long Next(string deviceId)
    {
        lock (_gate)
        {
            if (!_devices.TryGetValue(deviceId, out var state))
            {
                var persistedHigh = _reservedHighs.GetValueOrDefault(deviceId);
                var newHigh = checked(persistedHigh + BlockSize);
                _reservedHighs[deviceId] = newHigh;
                Persist();
                state = new DeviceSequence(persistedHigh, newHigh);
                _devices[deviceId] = state;
            }
            else if (state.Current >= state.ReservedHigh)
            {
                var newHigh = checked(state.ReservedHigh + BlockSize);
                _reservedHighs[deviceId] = newHigh;
                Persist();
                state.ReservedHigh = newHigh;
            }

            state.Current++;
            return state.Current;
        }
    }

    private Dictionary<string, long> LoadState()
    {
        Directory.CreateDirectory(_stateDirectory);
        if (!File.Exists(_stateFile))
        {
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_stateFile);
            return JsonSerializer.Deserialize<Dictionary<string, long>>(json)
                   ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Sequence state file '{_stateFile}' is unreadable. Restore or remove it before starting the agent; deleting it may cause duplicate sequences.",
                ex);
        }
    }

    private void Persist()
    {
        var temporary = _stateFile + ".tmp";
        var json = JsonSerializer.Serialize(
            _reservedHighs,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporary, json);
        File.Move(temporary, _stateFile, overwrite: true);
        _logger.LogDebug("Persisted sequence high-water marks for {Count} devices.", _reservedHighs.Count);
    }

    private sealed class DeviceSequence
    {
        public DeviceSequence(long current, long reservedHigh)
        {
            Current = current;
            ReservedHigh = reservedHigh;
        }

        public long Current { get; set; }

        public long ReservedHigh { get; set; }
    }
}
