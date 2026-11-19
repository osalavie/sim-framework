using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Sim.DeviceAgent.Configuration;

namespace Sim.DeviceAgent.Sources;

public sealed class FileSource : IDeviceSource
{
    private readonly SourceConfig _config;
    private readonly ILogger<FileSource> _logger;
    private readonly Dictionary<string, FileProbe> _probes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _processedKeys = new(StringComparer.OrdinalIgnoreCase);
    private SourceState _lastState = SourceState.Disconnected;

    public FileSource(SourceConfig config, ILogger<FileSource> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string Id => _config.Id;

    public bool CanWrite => false;

    public async Task RunAsync(SourceEventHandler handler, CancellationToken cancellationToken)
    {
        var pollInterval = TimeSpan.FromSeconds(_config.PollIntervalSeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                EnsureDirectories();
                await SetStateAsync(handler, SourceState.Connected, null, cancellationToken);
                await ScanAsync(handler, cancellationToken);
                await Task.Delay(pollInterval, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "File source {SourceId} cannot scan the input directory; retrying.", Id);
                await SetStateAsync(handler, SourceState.Waiting, ex.Message, cancellationToken);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        await SetStateAsync(handler, SourceState.Stopped, null, CancellationToken.None);
    }

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        => throw new NotSupportedException($"File source '{Id}' is read-only.");

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task ScanAsync(SourceEventHandler handler, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var files = Directory.EnumerateFiles(_config.Directory!, _config.Pattern, SearchOption.TopDirectoryOnly)
            .ToArray();

        var currentPaths = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        foreach (var stale in _probes.Keys.Where(path => !currentPaths.Contains(path)).ToArray())
        {
            _probes.Remove(stale);
        }

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileInfo file;
            try
            {
                file = new FileInfo(path);
                if (!file.Exists)
                {
                    continue;
                }
            }
            catch (IOException)
            {
                continue;
            }

            if (!_probes.TryGetValue(path, out var probe)
                || probe.Length != file.Length
                || probe.LastWriteUtc != file.LastWriteTimeUtc)
            {
                _probes[path] = new FileProbe(file.Length, file.LastWriteTimeUtc, now);
                continue;
            }

            if (now - probe.FirstSeen < TimeSpan.FromSeconds(_config.StableSeconds))
            {
                continue;
            }

            string hash;
            try
            {
                hash = await ComputeSha256Async(path, cancellationToken);
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "File source {SourceId} could not read {File}; it will be retried.", Id, path);
                _probes.Remove(path);
                continue;
            }

            var processedKey = $"{Path.GetFileName(path)}|{hash}";
            if (_processedKeys.Contains(processedKey))
            {
                TryArchive(path);
                _probes.Remove(path);
                continue;
            }

            var metadata = new FileMetadata(Path.GetFileName(path), file.Length, hash, file.LastWriteTimeUtc);
            SourceEventResult result;
            try
            {
                result = await handler(new SourceFileReady(path, metadata), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File source {SourceId} failed to process {File}.", Id, path);
                result = SourceEventResult.Fail(ex.Message);
            }

            _processedKeys.Add(processedKey);
            _probes.Remove(path);

            if (result.Success)
            {
                TryArchive(path);
            }
            else
            {
                _logger.LogWarning("File source {SourceId} rejected {File}: {Error}", Id, path, result.Error);
                TryMove(path, _config.ErrorDirectory!);
            }

            if (_processedKeys.Count > 10000)
            {
                _processedKeys.Clear();
            }
        }
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(_config.Directory!);
        Directory.CreateDirectory(_config.ArchiveDirectory!);
        Directory.CreateDirectory(_config.ErrorDirectory!);
    }

    private async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 8192,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void TryArchive(string path)
        => TryMove(path, Path.Combine(_config.ArchiveDirectory!, DateTime.Now.ToString("yyyyMMdd")));

    private void TryMove(string sourcePath, string destinationDirectory)
    {
        try
        {
            Directory.CreateDirectory(destinationDirectory);
            var destination = GetUniquePath(destinationDirectory, Path.GetFileName(sourcePath));
            File.Move(sourcePath, destination);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File source {SourceId} could not move {File} to {Directory}.", Id, sourcePath, destinationDirectory);
        }
    }

    private static string GetUniquePath(string directory, string fileName)
    {
        var destination = Path.Combine(directory, fileName);
        if (!File.Exists(destination))
        {
            return destination;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return Path.Combine(directory, $"{stem}_{DateTime.Now:yyyyMMddHHmmssfff}{extension}");
    }

    private async Task SetStateAsync(
        SourceEventHandler handler,
        SourceState state,
        string? detail,
        CancellationToken cancellationToken)
    {
        if (_lastState == state)
        {
            return;
        }

        _lastState = state;
        try
        {
            await handler(new SourceStateChanged(state, detail), cancellationToken);
        }
        catch when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown path.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "File source {SourceId} state notification failed.", Id);
        }
    }

    private sealed record FileProbe(long Length, DateTime LastWriteUtc, DateTimeOffset FirstSeen);
}
