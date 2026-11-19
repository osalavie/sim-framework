using Microsoft.Extensions.Logging;
using Sim.DeviceAgent.Configuration;

namespace Sim.DeviceAgent.Sources;

public static class SourceFactory
{
    public static IDeviceSource Create(SourceConfig config, ILoggerFactory loggerFactory)
    {
        return config.Type.ToLowerInvariant() switch
        {
            "serial" => new SerialSource(config, loggerFactory.CreateLogger<SerialSource>()),
            "tcpclient" => new TcpClientSource(config, loggerFactory.CreateLogger<TcpClientSource>()),
            "file" => new FileSource(config, loggerFactory.CreateLogger<FileSource>()),
            _ => throw new NotSupportedException($"Unsupported source type '{config.Type}'.")
        };
    }
}
