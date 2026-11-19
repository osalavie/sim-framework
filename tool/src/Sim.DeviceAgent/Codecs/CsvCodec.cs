using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Sim.DeviceAgent.Contracts;
using Sim.DeviceAgent.Sources;

namespace Sim.DeviceAgent.Codecs;

public sealed class CsvCodec : DeviceCodecBase
{
    private readonly Encoding _encoding;
    private readonly char _delimiter;
    private readonly bool _hasHeader;
    private readonly IReadOnlyDictionary<string, string> _columns;

    public CsvCodec(
        Encoding encoding,
        char delimiter,
        bool hasHeader,
        IReadOnlyDictionary<string, string>? columns)
    {
        _encoding = encoding;
        _delimiter = delimiter;
        _hasHeader = hasHeader;
        _columns = columns ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public override string Type => "csv";

    public override IFrameDecoder CreateDecoder()
        => throw new NotSupportedException("CSV codec is supported only for file sources.");

    public override async IAsyncEnumerable<DecodedFrame> DecodeFileAsync(
        Stream stream,
        FileMetadata metadata,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(
            stream,
            _encoding,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        if (!_hasHeader)
        {
            throw new InvalidDataException("CSV file source requires hasHeader=true in this MVP.");
        }

        var headerLine = await reader.ReadLineAsync(cancellationToken);
        if (headerLine is null)
        {
            yield break;
        }

        var headers = CsvLineParser.ParseLine(headerLine, _delimiter)
            .Select(header => header.Trim())
            .ToList();
        var map = BuildColumnMap(headers);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var raw = _encoding.GetBytes(line);
            var fields = CsvLineParser.ParseLine(line, _delimiter);
            if (fields.Count != headers.Count)
            {
                yield return new DecodedFrame(
                    raw,
                    line,
                    null,
                    $"csv_field_count_mismatch: expected {headers.Count}, got {fields.Count}");
                continue;
            }

            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (key, index) in map)
            {
                values[key] = ValueCoercion.Coerce(fields[index]);
            }

            yield return new DecodedFrame(
                raw,
                line,
                JsonSerializer.SerializeToElement(values, AgentJson.Options),
                null);
        }
    }

    private List<(string Key, int Index)> BuildColumnMap(IReadOnlyList<string> headers)
    {
        var map = new List<(string Key, int Index)>();
        if (_columns.Count == 0)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                map.Add((headers[i], i));
            }

            return map;
        }

        foreach (var (key, headerName) in _columns)
        {
            var index = -1;
            for (var i = 0; i < headers.Count; i++)
            {
                if (string.Equals(headers[i], headerName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                throw new InvalidDataException($"CSV header '{headerName}' for column '{key}' was not found.");
            }

            map.Add((key, index));
        }

        return map;
    }
}
