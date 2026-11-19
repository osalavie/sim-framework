using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sim.DeviceAgent.Contracts;

namespace Sim.DeviceAgent.Codecs;

public static class FrameParser
{
    public static DecodedFrame Parse(byte[] raw, Encoding encoding, Regex? parser)
    {
        var text = encoding.GetString(raw);
        if (parser is null)
        {
            return new DecodedFrame(raw, text, null, null);
        }

        try
        {
            var match = parser.Match(text);
            if (!match.Success)
            {
                return new DecodedFrame(raw, text, null, "regex_no_match");
            }

            var groupNames = parser.GetGroupNames()
                .Where(name => !int.TryParse(name, out _))
                .ToArray();

            if (groupNames.Length == 0)
            {
                return new DecodedFrame(raw, text, JsonSerializer.SerializeToElement(text, AgentJson.Options), null);
            }

            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var name in groupNames)
            {
                var group = match.Groups[name];
                values[name] = group.Success ? ValueCoercion.Coerce(group.Value) : null;
            }

            return new DecodedFrame(
                raw,
                text,
                JsonSerializer.SerializeToElement(values, AgentJson.Options),
                null);
        }
        catch (RegexMatchTimeoutException)
        {
            return new DecodedFrame(raw, text, null, "regex_timeout");
        }
    }
}
