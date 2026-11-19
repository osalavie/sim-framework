using Sim.DeviceAgent.Configuration;
using Xunit;

namespace Sim.DeviceAgent.Tests;

public sealed class ConfigValidatorTests
{
    [Fact]
    public void ValidFileChannel_HasNoErrors()
    {
        var errors = AgentConfigValidator.Validate(CreateValidConfig());

        Assert.Empty(errors);
    }

    [Fact]
    public void ExampleConfig_LoadsAndValidates()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "agent.example.json");

        var config = AgentConfigLoader.Load(path);
        var errors = AgentConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Fact]
    public void CenterExampleConfig_LoadsAndValidates()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "agent.center.example.json");

        var config = AgentConfigLoader.Load(path);
        var errors = AgentConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Fact]
    public void DuplicateDeviceId_IsRejected()
    {
        var config = CreateValidConfig();
        config.Channels.Add(new ChannelConfig
        {
            Id = "pt-file-2",
            DeviceId = "PT-001",
            DeviceType = "pullTest",
            SourceRef = "files",
            Codec = new CodecConfig { Type = "csv" },
            Sinks = ["local"]
        });

        var errors = AgentConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("Duplicate deviceId", StringComparison.Ordinal));
    }

    [Fact]
    public void SameSourceForTwoChannels_IsRejected()
    {
        var config = CreateValidConfig();
        config.Channels.Add(new ChannelConfig
        {
            Id = "pt-file-2",
            DeviceId = "PT-002",
            DeviceType = "pullTest",
            SourceRef = "files",
            Codec = new CodecConfig { Type = "csv" },
            Sinks = ["local"]
        });

        var errors = AgentConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("exactly one device channel", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownSink_IsRejected()
    {
        var config = CreateValidConfig();
        config.Channels[0].Sinks = ["missing"];

        var errors = AgentConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("unknown sink", StringComparison.Ordinal));
    }

    [Fact]
    public void CsvCodec_IsRejectedForSerialSource()
    {
        var config = CreateValidConfig();
        config.Sources[0] = new SourceConfig
        {
            Id = "files",
            Type = "serial",
            Port = "COM9"
        };

        var errors = AgentConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("only for file sources", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidRegex_IsRejected()
    {
        var config = CreateValidConfig();
        config.Sources[0] = new SourceConfig
        {
            Id = "files",
            Type = "serial",
            Port = "COM9"
        };
        config.Channels[0].Codec = new CodecConfig
        {
            Type = "delimiter",
            Delimiter = "\r\n",
            Parse = new RegexParseOptions { Pattern = "(" }
        };

        var errors = AgentConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("regex", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NonLoopbackBind_IsRejected()
    {
        var config = CreateValidConfig();
        config.Agent.LocalWs.Bind = "0.0.0.0";

        var errors = AgentConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("loopback", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyLocalToken_IsAllowed()
    {
        var config = CreateValidConfig();
        config.Agent.LocalWs.Token = string.Empty;

        var errors = AgentConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidCenterSink_HasNoErrors()
    {
        var config = CreateValidConfig();
        config.Sinks["center"] = new SinkConfig
        {
            Type = "wsClient",
            Url = "wss://center.example.com/device-gateway",
            Token = "center-token"
        };
        config.Channels[0].Sinks = ["local", "center"];

        var errors = AgentConfigValidator.Validate(config);

        Assert.Empty(errors);
    }

    [Fact]
    public void CenterSink_RequiresWebSocketUrl_ButTokenIsOptional()
    {
        var config = CreateValidConfig();
        config.Sinks["center"] = new SinkConfig
        {
            Type = "wsClient",
            Url = "https://center.example.com/device-gateway",
            Token = string.Empty
        };

        var errors = AgentConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("ws:// or wss://", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("requires token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnsafeDeviceId_IsRejected()
    {
        var config = CreateValidConfig();
        config.Channels[0].DeviceId = "../PT-001";

        var errors = AgentConfigValidator.Validate(config);

        Assert.Contains(errors, error => error.Contains("deviceId must use", StringComparison.Ordinal));
    }

    private static AgentConfig CreateValidConfig()
    {
        return new AgentConfig
        {
            Agent = new AgentOptions
            {
                Id = "agent-test",
                DisplayName = "Test agent",
                LocalWs = new LocalWsOptions
                {
                    Enabled = true,
                    Bind = "127.0.0.1",
                    Port = 18080,
                    Token = "test-token",
                    AllowedOrigins = ["http://localhost:5173"]
                }
            },
            Sources =
            [
                new SourceConfig
                {
                    Id = "files",
                    Type = "file",
                    Directory = "./in",
                    Pattern = "*.csv",
                    StableSeconds = 1,
                    PollIntervalSeconds = 1,
                    ArchiveDirectory = "./archive",
                    ErrorDirectory = "./error"
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
                    Id = "pt-file",
                    DeviceId = "PT-001",
                    DeviceType = "pullTest",
                    DeviceName = "Pull tester file",
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
}
