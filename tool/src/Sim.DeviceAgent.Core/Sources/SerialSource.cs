using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Sim.DeviceAgent.Configuration;

namespace Sim.DeviceAgent.Sources;

public sealed class SerialSource : IDeviceSource
{
    private readonly SourceConfig _config;
    private readonly ILogger<SerialSource> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private SerialPort? _port;

    public SerialSource(SourceConfig config, ILogger<SerialSource> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string Id => _config.Id;

    public bool CanWrite => true;

    public async Task RunAsync(SourceEventHandler handler, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            SerialPort? port = null;
            try
            {
                await NotifyAsync(handler, new SourceStateChanged(SourceState.Connecting), cancellationToken);
                port = CreatePort();
                port.Open();
                _port = port;
                await NotifyAsync(handler, new SourceStateChanged(SourceState.Connected), cancellationToken);

                using var registration = cancellationToken.Register(static state =>
                {
                    try
                    {
                        ((SerialPort)state!).Close();
                    }
                    catch
                    {
                        // Closing an already broken port is expected during shutdown.
                    }
                }, port);

                var buffer = new byte[4096];
                while (!cancellationToken.IsCancellationRequested)
                {
                    int read;
                    try
                    {
                        read = port.Read(buffer, 0, buffer.Length);
                    }
                    catch (TimeoutException)
                    {
                        continue;
                    }

                    if (read <= 0)
                    {
                        continue;
                    }

                    var result = await handler(new SourceBytes(buffer.AsSpan(0, read).ToArray()), cancellationToken);
                    if (!result.Success)
                    {
                        _logger.LogWarning("Serial source {SourceId} handler rejected a frame: {Error}", Id, result.Error);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Serial source {SourceId} is unavailable; retrying.", Id);
                await NotifyAsync(handler, new SourceStateChanged(SourceState.Waiting, ex.Message), cancellationToken);
            }
            finally
            {
                _port = null;
                try
                {
                    port?.Dispose();
                }
                catch
                {
                    // Ignore disposal failures for a disconnected device.
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await NotifyAsync(handler, new SourceStateChanged(SourceState.Stopped), CancellationToken.None);
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var port = _port ?? throw new InvalidOperationException($"Serial source '{Id}' is not connected.");
            port.Write(data.ToArray(), 0, data.Length);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _port?.Dispose();
        }
        catch
        {
            // Ignore disposal failures.
        }

        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }

    private SerialPort CreatePort()
    {
        var parity = Enum.Parse<Parity>(_config.Parity, ignoreCase: true);
        var stopBits = Enum.Parse<StopBits>(_config.StopBits, ignoreCase: true);
        return new SerialPort(_config.Port, _config.BaudRate, parity, _config.DataBits, stopBits)
        {
            ReadTimeout = _config.ReadTimeoutMs,
            WriteTimeout = 2000,
            DtrEnable = _config.DtrEnable,
            RtsEnable = _config.RtsEnable,
            Handshake = Handshake.None
        };
    }

    private static async Task NotifyAsync(
        SourceEventHandler handler,
        SourceStateChanged state,
        CancellationToken cancellationToken)
    {
        try
        {
            await handler(state, cancellationToken);
        }
        catch when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown path.
        }
        catch
        {
            // State notifications must not take down the source loop.
        }
    }
}
