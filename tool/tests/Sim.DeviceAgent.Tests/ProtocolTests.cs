using System.Text.Json;
using Sim.DeviceAgent.Contracts;
using Sim.DeviceAgent.WebSockets;
using Xunit;

namespace Sim.DeviceAgent.Tests;

public sealed class ProtocolTests
{
    [Fact]
    public void Subscription_AllMatchesEveryEnvelope()
    {
        var envelope = CreateEnvelope();

        Assert.True(ClientSubscription.All.Matches(envelope));
    }

    [Fact]
    public void Subscription_FiltersByDeviceTypeAndChannel()
    {
        var envelope = CreateEnvelope();

        Assert.True(new ClientSubscription(["pt-01"], ["PT-001"], ["pullTest"]).Matches(envelope));
        Assert.False(new ClientSubscription(["ds-01"], null, null).Matches(envelope));
        Assert.False(new ClientSubscription(null, ["DS-001"], null).Matches(envelope));
        Assert.False(new ClientSubscription(null, null, ["dieShear"]).Matches(envelope));
    }

    [Fact]
    public void TokenMatching_IsOptionalForLoopbackUse()
    {
        Assert.True(AgentTokenAuth.TokenMatches(null, string.Empty));
        Assert.True(AgentTokenAuth.TokenMatches("anything", string.Empty));
        Assert.True(AgentTokenAuth.TokenMatches("secret", "secret"));
        Assert.False(AgentTokenAuth.TokenMatches(null, "secret"));
        Assert.False(AgentTokenAuth.TokenMatches("wrong", "secret"));
    }

    [Fact]
    public void Envelope_SerializesWithCamelCaseAndOmitsNulls()
    {
        var envelope = CreateEnvelope();

        var json = JsonSerializer.Serialize(envelope, AgentJson.Options);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(MessageTypes.Data, root.GetProperty("type").GetString());
        Assert.Equal("agent-test", root.GetProperty("agentId").GetString());
        Assert.Equal("PT-001", root.GetProperty("deviceId").GetString());
        Assert.Equal("pullTest", root.GetProperty("deviceType").GetString());
        Assert.Equal("pt-01", root.GetProperty("channelId").GetString());
        Assert.Equal(7, root.GetProperty("seq").GetInt64());
        Assert.False(root.TryGetProperty("text", out _));
        Assert.False(root.TryGetProperty("error", out _));
    }

    private static MessageEnvelope CreateEnvelope()
    {
        return new MessageEnvelope
        {
            Type = MessageTypes.Data,
            AgentId = "agent-test",
            DeviceId = "PT-001",
            DeviceType = "pullTest",
            ChannelId = "pt-01",
            Seq = 7,
            Ts = DateTimeOffset.UtcNow
        };
    }
}
