using System.Text.Json;

namespace Sim.DeviceAgent.Configuration;

public static class AgentConfigLoader
{
    public const string ConfigPathEnvironmentVariable = "SIM_DEVICE_AGENT_CONFIG";
    public const string TokenEnvironmentVariable = "SIM_DEVICE_AGENT_TOKEN";
    public const string CenterTokenEnvironmentVariable = "SIM_DEVICE_AGENT_CENTER_TOKEN";

    public static AgentConfig Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Agent configuration file was not found: {fullPath}", fullPath);
        }

        var json = File.ReadAllText(fullPath);
        var config = JsonSerializer.Deserialize<AgentConfig>(json, JsonDefaults.Options)
                     ?? throw new InvalidOperationException("Agent configuration is empty.");

        config.Agent ??= new AgentOptions();
        config.Agent.Tray ??= new TrayOptions();
        config.Agent.LocalWs ??= new LocalWsOptions();
        config.Sources ??= [];
        config.Sinks ??= new Dictionary<string, SinkConfig>(StringComparer.OrdinalIgnoreCase);
        if (config.Sinks.Count > 0)
        {
            config.Sinks = new Dictionary<string, SinkConfig>(
                config.Sinks,
                StringComparer.OrdinalIgnoreCase);
        }

        var envToken = Environment.GetEnvironmentVariable(TokenEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envToken))
        {
            config.Agent.LocalWs.Token = envToken;
        }

        var centerToken = Environment.GetEnvironmentVariable(CenterTokenEnvironmentVariable);
        foreach (var sink in config.Sinks.Values)
        {
            if (sink is not null &&
                string.Equals(sink.Type, "wsClient", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(centerToken))
            {
                sink.Token = centerToken;
            }
        }

        ResolveRelativeDirectories(config, Path.GetDirectoryName(fullPath)!);
        return config;
    }

    public static string ResolveConfigPath(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--config" or "-c" && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            if (args[i].StartsWith("--config=", StringComparison.Ordinal))
            {
                return args[i]["--config=".Length..];
            }
        }

        return Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable)
               ?? Path.Combine(AppContext.BaseDirectory, "agent.json");
    }

    private static void ResolveRelativeDirectories(AgentConfig config, string baseDirectory)
    {
        config.Agent.StateDirectory = ResolvePath(config.Agent.StateDirectory, baseDirectory)
                                      ?? Path.Combine(baseDirectory, "data");

        foreach (var source in config.Sources)
        {
            if (source is null)
            {
                continue;
            }

            source.Directory = ResolvePath(source.Directory, baseDirectory);
            source.ArchiveDirectory = ResolvePath(source.ArchiveDirectory, baseDirectory);
            source.ErrorDirectory = ResolvePath(source.ErrorDirectory, baseDirectory);
        }

        foreach (var sink in config.Sinks.Values)
        {
            if (sink is not null)
            {
                sink.SpoolDirectory = ResolvePath(sink.SpoolDirectory, baseDirectory);
            }
        }
    }

    private static string? ResolvePath(string? path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(baseDirectory, path));
    }

    private static class JsonDefaults
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }
}
