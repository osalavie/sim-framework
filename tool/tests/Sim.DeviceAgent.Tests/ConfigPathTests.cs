using Sim.DeviceAgent.Configuration;
using Xunit;

namespace Sim.DeviceAgent.Tests;

public sealed class ConfigPathTests
{
    [Fact]
    public void CommandLineConfig_WinsOverDefault()
    {
        Assert.Equal("custom.json", AgentConfigLoader.ResolveConfigPath(["--config", "custom.json"]));
        Assert.Equal("custom.json", AgentConfigLoader.ResolveConfigPath(["--config=custom.json"]));
        Assert.Equal("custom.json", AgentConfigLoader.ResolveConfigPath(["-c", "custom.json"]));
    }

    [Fact]
    public void NoArgument_UsesAgentJsonNextToProgram()
    {
        var previous = Environment.GetEnvironmentVariable(AgentConfigLoader.ConfigPathEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(AgentConfigLoader.ConfigPathEnvironmentVariable, null);

            var path = AgentConfigLoader.ResolveConfigPath([]);

            Assert.Equal(Path.Combine(AppContext.BaseDirectory, "agent.json"), path);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AgentConfigLoader.ConfigPathEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void EnvironmentVariable_WinsOverDefault()
    {
        var previous = Environment.GetEnvironmentVariable(AgentConfigLoader.ConfigPathEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(AgentConfigLoader.ConfigPathEnvironmentVariable, "env-agent.json");

            Assert.Equal("env-agent.json", AgentConfigLoader.ResolveConfigPath([]));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AgentConfigLoader.ConfigPathEnvironmentVariable, previous);
        }
    }
}
