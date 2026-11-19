using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sim.DeviceAgent.Contracts;

public sealed class MessageEnvelope
{
    public int V { get; init; } = 1;

    public string Type { get; init; } = string.Empty;

    public string? AgentId { get; set; }

    public string? DeviceId { get; set; }

    public string? DeviceType { get; set; }

    public string? ChannelId { get; set; }

    public long? Seq { get; set; }

    public DateTimeOffset? Ts { get; set; }

    public string? Raw { get; set; }

    public string? Text { get; set; }

    public JsonElement? Parsed { get; set; }

    public string? RequestId { get; set; }

    public SourceRef? SourceRef { get; set; }

    public MessageError? Error { get; set; }

    public string? State { get; set; }

    public JsonElement? Payload { get; set; }

    public bool? Ok { get; set; }
}

public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
