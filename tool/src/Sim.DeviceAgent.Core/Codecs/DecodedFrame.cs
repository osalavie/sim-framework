using System.Text.Json;

namespace Sim.DeviceAgent.Codecs;

public sealed record DecodedFrame(byte[] Raw, string? Text, JsonElement? Parsed, string? ParseError);
