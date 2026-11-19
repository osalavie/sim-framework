using System.Text.RegularExpressions;
using Sim.DeviceAgent.Configuration;

namespace Sim.DeviceAgent.Codecs;

public static class CodecFactory
{
    public static IDeviceCodec Create(CodecConfig config, string sourceType)
    {
        var encoding = EncodingResolver.Resolve(config.Encoding);

        return config.Type.ToLowerInvariant() switch
        {
            "delimiter" => CreateDelimiter(config, encoding),
            "passthrough" => new PassthroughCodec(encoding),
            "csv" => CreateCsv(config, encoding, sourceType),
            _ => throw new NotSupportedException($"Unsupported codec type '{config.Type}'.")
        };
    }

    private static IDeviceCodec CreateDelimiter(CodecConfig config, System.Text.Encoding encoding)
    {
        Regex? parser = null;
        if (!string.IsNullOrWhiteSpace(config.Parse?.Pattern))
        {
            parser = new Regex(
                config.Parse.Pattern,
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(250));
        }

        return new DelimiterCodec(config.Delimiter, encoding, parser, config.MaxFrameBytes);
    }

    private static IDeviceCodec CreateCsv(CodecConfig config, System.Text.Encoding encoding, string sourceType)
    {
        if (!string.Equals(sourceType, "file", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("CSV codec is supported only for file sources.");
        }

        if (config.CsvDelimiter.Length != 1)
        {
            throw new ArgumentException("CSV delimiter must be exactly one character.", nameof(config));
        }

        return new CsvCodec(
            encoding,
            config.CsvDelimiter[0],
            config.HasHeader,
            config.Columns);
    }
}
