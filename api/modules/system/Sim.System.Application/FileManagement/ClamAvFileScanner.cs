using System.Net.Sockets;
using Sim.System.FileManagement;
using Microsoft.Extensions.Logging;

namespace Sim.System.Application.FileManagement;

/// <summary>
/// Scans file content for malware using a ClamAV daemon via TCP (INSTREAM command).
/// Connects to clamd on the configured host:port (default localhost:3310).
/// </summary>
public sealed class ClamAvFileScanner : IFileScanner
{
    private readonly ILogger<ClamAvFileScanner> _logger;
    private readonly ClamAvOptions _options;

    public string Name => "ClamAV";

    public ClamAvFileScanner(ILogger<ClamAvFileScanner> logger, ClamAvOptions options)
    {
        _logger = logger;
        _options = options;
    }

    public async Task<string?> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (!content.CanSeek)
            throw new ArgumentException("ClamAV scanner requires a seekable stream.");

        var originalPosition = content.Position;

        try
        {
            content.Position = 0;

            using var client = new TcpClient();
            var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(_options.ConnectTimeoutMs > 0 ? _options.ConnectTimeoutMs : 5000);

            await client.ConnectAsync(_options.Host, _options.Port, connectCts.Token);

            using var networkStream = client.GetStream();
            networkStream.ReadTimeout = _options.ReadTimeoutMs > 0 ? _options.ReadTimeoutMs : 30000;
            networkStream.WriteTimeout = _options.WriteTimeoutMs > 0 ? _options.WriteTimeoutMs : 30000;

            // Send INSTREAM command
            var instreamCmd = "zINSTREAM\0"u8.ToArray();
            await networkStream.WriteAsync(instreamCmd, cancellationToken);

            // Send file content: 4-byte big-endian length + data chunk, terminated by zero-length chunk
            var buffer = new byte[_options.ChunkSize > 0 ? _options.ChunkSize : 4096];
            int bytesRead;
            while ((bytesRead = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                // Write chunk size (big-endian uint32)
                var sizeBytes = BitConverter.GetBytes((uint)bytesRead);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(sizeBytes);

                await networkStream.WriteAsync(sizeBytes, cancellationToken);
                await networkStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            // Terminating zero-length chunk
            await networkStream.WriteAsync(new byte[4], cancellationToken); // 4 zero bytes = size 0
            await networkStream.FlushAsync(cancellationToken);

            // Read response
            using var reader = new StreamReader(networkStream, leaveOpen: true);
            var response = await reader.ReadLineAsync(cancellationToken);

            _logger.LogDebug("ClamAV scan result: {Response}", response);

            if (string.IsNullOrWhiteSpace(response))
                return "Empty ClamAV response";

            if (response.StartsWith("stream: OK", StringComparison.Ordinal))
                return null; // Clean

            // Extract virus name: "stream: Eicar-Test-Signature FOUND"
            if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                var virusName = response.Replace("stream:", "").Replace("FOUND", "").Trim();
                _logger.LogWarning("ClamAV detected malware: {VirusName}", virusName);
                return virusName;
            }

            if (response.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("ClamAV scan error: {Response}", response);
                return $"ClamAV error: {response}";
            }

            return $"Unknown ClamAV response: {response}";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("ClamAV connection timeout to {Host}:{Port}", _options.Host, _options.Port);
            return "ClamAV connection timeout";
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "ClamAV socket error connecting to {Host}:{Port}", _options.Host, _options.Port);
            return $"ClamAV socket error: {ex.Message}";
        }
        finally
        {
            if (content.CanSeek)
                content.Position = originalPosition;
        }
    }
}

/// <summary>
/// Configuration options for the ClamAV file scanner.
/// </summary>
public sealed class ClamAvOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public int ConnectTimeoutMs { get; set; } = 5000;
    public int ReadTimeoutMs { get; set; } = 30000;
    public int WriteTimeoutMs { get; set; } = 30000;
    public int ChunkSize { get; set; } = 4096;
}
