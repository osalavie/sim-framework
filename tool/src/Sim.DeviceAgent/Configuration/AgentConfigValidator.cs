using System.IO.Ports;
using System.Net;
using System.Text.RegularExpressions;
using Sim.DeviceAgent.Codecs;

namespace Sim.DeviceAgent.Configuration;

public static class AgentConfigValidator
{
    private static readonly HashSet<string> SupportedSourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "serial",
        "tcpClient",
        "file"
    };

    private static readonly HashSet<string> SupportedCodecTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "delimiter",
        "passthrough",
        "csv"
    };

    public static IReadOnlyList<string> Validate(AgentConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var errors = new List<string>();

        config.Agent ??= new AgentOptions();
        config.Sources ??= [];
        config.Sinks ??= new Dictionary<string, SinkConfig>(StringComparer.OrdinalIgnoreCase);
        config.Channels ??= [];

        ValidateAgent(config.Agent, errors);

        var sources = new Dictionary<string, SourceConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in config.Sources)
        {
            if (source is null)
            {
                errors.Add("Source entry is null.");
                continue;
            }

            ValidateSource(source, sources, errors);
        }

        foreach (var sink in config.Sinks)
        {
            if (string.IsNullOrWhiteSpace(sink.Key))
            {
                errors.Add("Sink id cannot be empty.");
            }

            if (sink.Value is null)
            {
                errors.Add($"Sink '{sink.Key}' is null.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(sink.Key) && !IsSafePathSegment(sink.Key))
            {
                errors.Add($"Sink id '{sink.Key}' must use only letters, digits, '.', '_' or '-'.");
            }

            switch (sink.Value.Type.ToLowerInvariant())
            {
                case "wsserver":
                    break;

                case "wsclient":
                    ValidateCenterSink(sink.Key, sink.Value, errors);
                    break;

                default:
                    errors.Add($"Sink '{sink.Key}' uses unsupported type '{sink.Value.Type}'. Supported values: wsServer, wsClient.");
                    break;
            }
        }

        var channelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var enabledChannelCount = 0;

        foreach (var channel in config.Channels)
        {
            if (channel is null)
            {
                errors.Add("Channel entry is null.");
                continue;
            }

            if (!channel.Enabled)
            {
                continue;
            }

            enabledChannelCount++;
            var channelLabel = string.IsNullOrWhiteSpace(channel.Id) ? "<unnamed>" : channel.Id;

            if (string.IsNullOrWhiteSpace(channel.Id))
            {
                errors.Add("Channel id cannot be empty.");
            }
            else if (!channelIds.Add(channel.Id))
            {
                errors.Add($"Duplicate channel id '{channel.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(channel.DeviceId))
            {
                errors.Add($"Channel '{channelLabel}' requires deviceId.");
            }
            else if (!IsSafePathSegment(channel.DeviceId))
            {
                errors.Add($"Channel '{channelLabel}' deviceId must use only letters, digits, '.', '_' or '-'.");
            }
            else if (!deviceIds.Add(channel.DeviceId))
            {
                errors.Add($"Duplicate deviceId '{channel.DeviceId}'. Each device must have exactly one channel.");
            }

            if (string.IsNullOrWhiteSpace(channel.DeviceType))
            {
                errors.Add($"Channel '{channelLabel}' requires deviceType.");
            }

            if (string.IsNullOrWhiteSpace(channel.SourceRef))
            {
                errors.Add($"Channel '{channelLabel}' requires sourceRef.");
            }
            else if (!sources.TryGetValue(channel.SourceRef, out var source))
            {
                errors.Add($"Channel '{channelLabel}' references unknown source '{channel.SourceRef}'.");
            }
            else
            {
                sourceUsage[channel.SourceRef] = sourceUsage.GetValueOrDefault(channel.SourceRef) + 1;
                ValidateChannelCodec(channel, source, errors);
            }

            channel.Sinks ??= [];
            if (channel.Sinks.Count == 0)
            {
                errors.Add($"Channel '{channelLabel}' must reference at least one sink.");
            }

            foreach (var sinkRef in channel.Sinks)
            {
                if (!config.Sinks.ContainsKey(sinkRef))
                {
                    errors.Add($"Channel '{channelLabel}' references unknown sink '{sinkRef}'.");
                }
            }

            if (channel.QueueCapacity < 16 || channel.QueueCapacity > 1_000_000)
            {
                errors.Add($"Channel '{channelLabel}' queueCapacity must be between 16 and 1000000.");
            }
        }

        foreach (var usage in sourceUsage)
        {
            if (usage.Value > 1)
            {
                errors.Add($"Source '{usage.Key}' is referenced by {usage.Value} channels. A source maps to exactly one device channel; split the physical link before the Agent.");
            }
        }

        if (enabledChannelCount == 0)
        {
            errors.Add("At least one enabled channel is required.");
        }

        return errors;
    }

    private static void ValidateAgent(AgentOptions agent, List<string> errors)
    {
        agent.Tray ??= new TrayOptions();

        if (string.IsNullOrWhiteSpace(agent.Id))
        {
            errors.Add("agent.id is required.");
        }
        else if (!IsSafePathSegment(agent.Id))
        {
            errors.Add("agent.id must use only letters, digits, '.', '_' or '-'.");
        }

        if (string.IsNullOrWhiteSpace(agent.StateDirectory))
        {
            errors.Add("agent.stateDirectory is required.");
        }

        var localWs = agent.LocalWs ??= new LocalWsOptions();
        if (!localWs.Enabled)
        {
            errors.Add("agent.localWs.enabled must be true in this MVP.");
        }

        if (!IsLoopback(localWs.Bind))
        {
            errors.Add($"agent.localWs.bind must be a loopback address. Received '{localWs.Bind}'.");
        }

        if (localWs.Port is < 1 or > 65535)
        {
            errors.Add("agent.localWs.port must be between 1 and 65535.");
        }

        localWs.AllowedOrigins ??= [];
        foreach (var origin in localWs.AllowedOrigins)
        {
            if (!IsValidOrigin(origin))
            {
                errors.Add($"agent.localWs.allowedOrigins contains an invalid origin '{origin}'.");
            }
        }
    }

    private static void ValidateSource(
        SourceConfig source,
        Dictionary<string, SourceConfig> sources,
        List<string> errors)
    {
        var label = string.IsNullOrWhiteSpace(source.Id) ? "<unnamed>" : source.Id;
        if (string.IsNullOrWhiteSpace(source.Id))
        {
            errors.Add("Source id cannot be empty.");
        }
        else if (!sources.TryAdd(source.Id, source))
        {
            errors.Add($"Duplicate source id '{source.Id}'.");
        }

        if (!SupportedSourceTypes.Contains(source.Type))
        {
            errors.Add($"Source '{label}' uses unsupported type '{source.Type}'. Supported values: serial, tcpClient, file.");
            return;
        }

        switch (source.Type.ToLowerInvariant())
        {
            case "serial":
                if (string.IsNullOrWhiteSpace(source.Port))
                {
                    errors.Add($"Serial source '{label}' requires port.");
                }

                if (source.BaudRate is < 300 or > 921600)
                {
                    errors.Add($"Serial source '{label}' baudRate must be between 300 and 921600.");
                }

                if (source.DataBits is < 5 or > 8)
                {
                    errors.Add($"Serial source '{label}' dataBits must be between 5 and 8.");
                }

                if (!Enum.TryParse<Parity>(source.Parity, ignoreCase: true, out _))
                {
                    errors.Add($"Serial source '{label}' parity must be None, Odd, Even, Mark or Space.");
                }

                if (!Enum.TryParse<StopBits>(source.StopBits, ignoreCase: true, out _))
                {
                    errors.Add($"Serial source '{label}' stopBits must be One, OnePointFive or Two.");
                }

                if (source.ReadTimeoutMs is < 50 or > 10000)
                {
                    errors.Add($"Serial source '{label}' readTimeoutMs must be between 50 and 10000.");
                }

                break;

            case "tcpclient":
                if (string.IsNullOrWhiteSpace(source.Host))
                {
                    errors.Add($"TCP source '{label}' requires host.");
                }

                if (!int.TryParse(source.Port, out var tcpPort) || tcpPort is < 1 or > 65535)
                {
                    errors.Add($"TCP source '{label}' requires a valid port between 1 and 65535.");
                }

                if (source.ReconnectMinSeconds < 1 || source.ReconnectMaxSeconds < source.ReconnectMinSeconds)
                {
                    errors.Add($"TCP source '{label}' reconnect range is invalid.");
                }

                break;

            case "file":
                if (string.IsNullOrWhiteSpace(source.Directory))
                {
                    errors.Add($"File source '{label}' requires directory.");
                }

                if (string.IsNullOrWhiteSpace(source.Pattern))
                {
                    errors.Add($"File source '{label}' requires pattern.");
                }

                if (string.IsNullOrWhiteSpace(source.ArchiveDirectory))
                {
                    errors.Add($"File source '{label}' requires archiveDirectory.");
                }

                if (string.IsNullOrWhiteSpace(source.ErrorDirectory))
                {
                    errors.Add($"File source '{label}' requires errorDirectory.");
                }

                if (source.StableSeconds < 0.1)
                {
                    errors.Add($"File source '{label}' stableSeconds must be at least 0.1.");
                }

                if (source.PollIntervalSeconds < 0.1)
                {
                    errors.Add($"File source '{label}' pollIntervalSeconds must be at least 0.1.");
                }

                if (!string.IsNullOrWhiteSpace(source.ArchiveDirectory)
                    && !string.IsNullOrWhiteSpace(source.ErrorDirectory)
                    && string.Equals(
                        Path.GetFullPath(source.ArchiveDirectory),
                        Path.GetFullPath(source.ErrorDirectory),
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"File source '{label}' archiveDirectory and errorDirectory must be different.");
                }

                break;
        }
    }

    private static void ValidateChannelCodec(ChannelConfig channel, SourceConfig source, List<string> errors)
    {
        var label = string.IsNullOrWhiteSpace(channel.Id) ? "<unnamed>" : channel.Id;
        var codec = channel.Codec ??= new CodecConfig();
        if (!SupportedCodecTypes.Contains(codec.Type))
        {
            errors.Add($"Channel '{label}' uses unsupported codec '{codec.Type}'. Supported values: delimiter, passthrough, csv.");
            return;
        }

        if (codec.MaxFrameBytes is < 64 or > 4 * 1024 * 1024)
        {
            errors.Add($"Channel '{label}' codec.maxFrameBytes must be between 64 and 4194304.");
        }

        try
        {
            _ = EncodingResolver.Resolve(codec.Encoding);
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Channel '{label}': {ex.Message}");
        }

        switch (codec.Type.ToLowerInvariant())
        {
            case "delimiter":
                if (string.IsNullOrEmpty(codec.Delimiter))
                {
                    errors.Add($"Channel '{label}' delimiter codec requires a non-empty delimiter.");
                }

                if (!string.IsNullOrWhiteSpace(codec.Parse?.Pattern))
                {
                    ValidateRegex(label, codec.Parse.Pattern, errors);
                }

                break;

            case "passthrough":
                break;

            case "csv":
                if (!string.Equals(source.Type, "file", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Channel '{label}' uses csv codec, which is supported only for file sources.");
                }

                if (!codec.HasHeader)
                {
                    errors.Add($"Channel '{label}' csv codec requires hasHeader=true in this MVP.");
                }

                codec.CsvDelimiter ??= string.Empty;
                if (codec.CsvDelimiter.Length != 1)
                {
                    errors.Add($"Channel '{label}' csv codec requires csvDelimiter to be exactly one character.");
                }

                if (codec.Columns is null)
                {
                    codec.Columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                foreach (var column in codec.Columns)
                {
                    if (string.IsNullOrWhiteSpace(column.Key) || string.IsNullOrWhiteSpace(column.Value))
                    {
                        errors.Add($"Channel '{label}' csv columns contain an empty key or header name.");
                    }
                }

                break;
        }
    }

    private static void ValidateRegex(string channelLabel, string pattern, List<string> errors)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Channel '{channelLabel}' regex parse pattern is invalid: {ex.Message}");
        }
    }

    private static void ValidateCenterSink(string sinkId, SinkConfig sink, List<string> errors)
    {
        if (!Uri.TryCreate(sink.Url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("ws" or "wss"))
        {
            errors.Add($"Center sink '{sinkId}' requires an absolute ws:// or wss:// url.");
        }

        if (sink.ReconnectMinSeconds < 1 || sink.ReconnectMaxSeconds < sink.ReconnectMinSeconds)
        {
            errors.Add($"Center sink '{sinkId}' reconnect range is invalid.");
        }

        if (sink.SpoolSegmentMegabytes is < 1 or > 1024)
        {
            errors.Add($"Center sink '{sinkId}' spoolSegmentMegabytes must be between 1 and 1024.");
        }

        if (sink.SpoolMaxMegabytes < sink.SpoolSegmentMegabytes || sink.SpoolMaxMegabytes > 1_048_576)
        {
            errors.Add($"Center sink '{sinkId}' spoolMaxMegabytes must be at least spoolSegmentMegabytes and at most 1048576.");
        }
    }

    private static bool IsSafePathSegment(string value)
    {
        return value.Length > 0 &&
               value is not "." and not ".." &&
               value.All(character =>
                   char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
    }

    private static bool IsLoopback(string? bind)
    {
        if (string.IsNullOrWhiteSpace(bind))
        {
            return false;
        }

        if (string.Equals(bind, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(bind, out var address) && IPAddress.IsLoopback(address);
    }

    private static bool IsValidOrigin(string? origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https"
               && string.IsNullOrEmpty(uri.Query)
               && uri.AbsolutePath == "/";
    }
}
