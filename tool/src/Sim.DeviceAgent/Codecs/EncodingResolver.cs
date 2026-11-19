using System.Text;

namespace Sim.DeviceAgent.Codecs;

public static class EncodingResolver
{
    public static Encoding Resolve(string? name)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? "utf-8" : name.Trim().ToLowerInvariant();
        return normalized switch
        {
            "utf-8" or "utf8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            "ascii" => Encoding.ASCII,
            "latin1" or "iso-8859-1" => Encoding.Latin1,
            "utf-16" or "unicode" => Encoding.Unicode,
            "utf-16-be" or "bigendianunicode" => Encoding.BigEndianUnicode,
            _ => throw new ArgumentException($"Unsupported encoding '{name}'. Supported values: utf-8, ascii, latin1, utf-16, utf-16-be.")
        };
    }
}
