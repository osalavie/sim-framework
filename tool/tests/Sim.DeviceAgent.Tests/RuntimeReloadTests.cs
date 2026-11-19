using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sim.DeviceAgent.Configuration;
using Sim.DeviceAgent.Runtime;
using Sim.DeviceAgent.WebSockets;
using Xunit;

namespace Sim.DeviceAgent.Tests;

public sealed class RuntimeReloadTests
{
    [Fact]
    public async Task RuntimeReload_ReplacesChannelsAndSinks_AndIsRejectedAfterStop()
    {
        var root = CreateTempDirectory();
        var config = CreateConfig(root, "PT-001", "pt-01", "in-a");
        var (runtime, registry, sinkRegistry) = CreateRuntime(root, config);

        try
        {
            await runtime.StartAsync(CancellationToken.None);

            Assert.True(runtime.IsRunning);
            Assert.Single(registry.GetStatuses());
            Assert.Equal("PT-001", registry.GetStatuses()[0].DeviceId);
            Assert.Single(sinkRegistry.GetStatuses());

            var candidate = CreateConfig(root, "PT-002", "pt-02", "in-b");
            await runtime.ReloadAsync(candidate, CancellationToken.None);

            var statuses = registry.GetStatuses();
            Assert.Single(statuses);
            Assert.Equal("PT-002", statuses[0].DeviceId);
            Assert.Equal("pt-02", statuses[0].ChannelId);
            Assert.Equal("local", Assert.Single(sinkRegistry.GetStatuses()).Id);
            Assert.True(runtime.IsRunning);
        }
        finally
        {
            await runtime.StopAsync(CancellationToken.None);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runtime.ReloadAsync(CreateConfig(root, "PT-003", "pt-03", "in-c"), CancellationToken.None));

        await runtime.DisposeAsync();
        DeleteDirectory(root);
    }

    [Fact]
    public async Task Reloader_RejectsChangesThatRequireRestart()
    {
        var root = CreateTempDirectory();
        var config = CreateConfig(root, "PT-001", "pt-01", "in-a");
        var configPath = Path.Combine(root, "agent.json");
        WriteConfig(configPath, config);

        var (runtime, _, _) = CreateRuntime(root, config);
        var reloader = new AgentConfigReloader(
            new AgentConfigFile(configPath),
            config,
            runtime,
            NullLogger<AgentConfigReloader>.Instance);

        try
        {
            var candidate = CreateConfig(root, "PT-001", "pt-01", "in-a");
            candidate.Agent.LocalWs.Port++;
            WriteConfig(configPath, candidate);

            var result = await reloader.ReloadAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("agent.localWs.port", result.Message, StringComparison.Ordinal);
            Assert.Contains("restart", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await runtime.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Reloader_KeepsRunningConfiguration_WhenFileIsInvalid()
    {
        var root = CreateTempDirectory();
        var config = CreateConfig(root, "PT-001", "pt-01", "in-a");
        var configPath = Path.Combine(root, "agent.json");
        WriteConfig(configPath, config);

        var (runtime, _, _) = CreateRuntime(root, config);
        var reloader = new AgentConfigReloader(
            new AgentConfigFile(configPath),
            config,
            runtime,
            NullLogger<AgentConfigReloader>.Instance);

        try
        {
            await File.WriteAllTextAsync(configPath, "{ not json");
            var result = await reloader.ReloadAsync(CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("could not be loaded", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await runtime.DisposeAsync();
            DeleteDirectory(root);
        }
    }

    private static (DeviceAgentRuntime Runtime, AgentChannelRegistry Registry, AgentSinkRegistry SinkRegistry)
        CreateRuntime(string root, AgentConfig config)
    {
        var registry = new AgentChannelRegistry();
        var sinkRegistry = new AgentSinkRegistry();
        var hub = new LocalWebSocketHub(config, registry, NullLogger<LocalWebSocketHub>.Instance);
        var sequences = new FileSequenceStore(
            Path.Combine(root, "state"),
            NullLogger<FileSequenceStore>.Instance);
        var runtime = new DeviceAgentRuntime(
            config,
            registry,
            sinkRegistry,
            hub,
            sequences,
            NullLoggerFactory.Instance,
            NullLogger<DeviceAgentRuntime>.Instance);

        return (runtime, registry, sinkRegistry);
    }

    private static AgentConfig CreateConfig(string root, string deviceId, string channelId, string directoryName)
    {
        var sourceDirectory = Path.Combine(root, directoryName);
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(Path.Combine(root, directoryName + "-archive"));
        Directory.CreateDirectory(Path.Combine(root, directoryName + "-error"));

        return new AgentConfig
        {
            Agent = new AgentOptions
            {
                Id = "agent-test",
                DisplayName = "Test agent",
                StateDirectory = Path.Combine(root, "state"),
                Tray = new TrayOptions { Enabled = false },
                LocalWs = new LocalWsOptions
                {
                    Enabled = true,
                    Bind = "127.0.0.1",
                    Port = 18080,
                    Token = string.Empty,
                    AllowedOrigins = ["http://localhost:5173"]
                }
            },
            Sources =
            [
                new SourceConfig
                {
                    Id = "files",
                    Type = "file",
                    Directory = sourceDirectory,
                    Pattern = "*.csv",
                    StableSeconds = 0.5,
                    PollIntervalSeconds = 0.5,
                    ArchiveDirectory = Path.Combine(root, directoryName + "-archive"),
                    ErrorDirectory = Path.Combine(root, directoryName + "-error")
                }
            ],
            Sinks = new Dictionary<string, SinkConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["local"] = new SinkConfig { Type = "wsServer" }
            },
            Channels =
            [
                new ChannelConfig
                {
                    Id = channelId,
                    DeviceId = deviceId,
                    DeviceType = "pullTest",
                    DeviceName = "Pull tester",
                    SourceRef = "files",
                    Codec = new CodecConfig
                    {
                        Type = "csv",
                        CsvDelimiter = ",",
                        HasHeader = true,
                        Columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["sampleId"] = "Sample",
                            ["force"] = "ForceN"
                        }
                    },
                    Sinks = ["local"]
                }
            ]
        };
    }

    private static void WriteConfig(string path, AgentConfig config)
    {
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "sim-device-agent-reload-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Test cleanup only.
        }
    }
}
