using System.Collections.Concurrent;
using Sim.System.FileManagement;
using Microsoft.Extensions.Logging;
using Sim.System.Domain.Shared;
using Sim.System.Domain.Shared.Enums;

namespace Sim.System.Application.FileManagement;

public sealed record FileQuarantineRecord(
    string Key,
    string Provider,
    QuarantineStatus Status,
    string? ScannerName,
    string? ScanResult,
    DateTimeOffset? ScannedAt);

/// <summary>
/// Scans a file for malicious content.
/// Implementations wrap external scanners like ClamAV.
/// </summary>
public interface IFileScanner
{
    /// <summary>Human-readable scanner name (e.g., "ClamAV").</summary>
    string Name { get; }

    /// <summary>
    /// Scans the file content.
    /// Returns null if clean, or a threat description if malicious content is found.
    /// Throws on scanner infrastructure error.
    /// </summary>
    Task<string?> ScanAsync(Stream content, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manages the quarantine lifecycle for uploaded files.
/// In-memory store suitable for single-instance deployments;
/// replace with a persistent store for production.
/// </summary>
public interface IQuarantineService
{
    /// <summary>Records a file for quarantine tracking.</summary>
    Task RegisterAsync(string key, string provider, CancellationToken cancellationToken = default);

    /// <summary>Updates the quarantine status after scanning.</summary>
    Task UpdateStatusAsync(string key, QuarantineStatus status, string? scanResult = null,
        string? scannerName = null, CancellationToken cancellationToken = default);

    /// <summary>Gets the current quarantine record for a file.</summary>
    Task<FileQuarantineRecord?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Lists all files with the given quarantine status.</summary>
    Task<IReadOnlyList<FileQuarantineRecord>> ListByStatusAsync(
        QuarantineStatus status, int limit = 100, CancellationToken cancellationToken = default);
}

public sealed class InMemoryQuarantineService(ILogger<InMemoryQuarantineService> logger) : IQuarantineService
{
    private readonly ConcurrentDictionary<string, FileQuarantineRecord> _records = new();

    public Task RegisterAsync(string key, string provider, CancellationToken cancellationToken = default)
    {
        var compositeKey = CompositeKey(key, provider);
        if (_records.TryAdd(compositeKey, new FileQuarantineRecord(
                key, provider, QuarantineStatus.Pending, null, null, null)))
        {
            logger.LogDebug("File {Key} registered for quarantine scan (provider: {Provider})", key, provider);
        }
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(string key, QuarantineStatus status, string? scanResult = null,
        string? scannerName = null, CancellationToken cancellationToken = default)
    {
        var compositeKey = CompositeKey(key, "unknown");
        // Find by key prefix
        var matched = _records.Keys.FirstOrDefault(k => k.StartsWith($"{key}|", StringComparison.Ordinal));
        if (matched is null)
        {
            logger.LogWarning("Attempted to update quarantine status for untracked file {Key}", key);
            return Task.CompletedTask;
        }

        var record = _records[matched];
        _records[matched] = record with
        {
            Status = status,
            ScannerName = scannerName,
            ScanResult = scanResult,
            ScannedAt = DateTimeOffset.UtcNow
        };
        logger.LogInformation("File {Key} quarantine status updated: {Status}", key, status);
        return Task.CompletedTask;
    }

    public Task<FileQuarantineRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var compositeKey = _records.Keys.FirstOrDefault(
            k => k.StartsWith($"{key}|", StringComparison.Ordinal));
        return Task.FromResult(compositeKey is not null ? _records[compositeKey] : null);
    }

    public Task<IReadOnlyList<FileQuarantineRecord>> ListByStatusAsync(
        QuarantineStatus status, int limit = 100, CancellationToken cancellationToken = default)
    {
        var results = _records.Values
            .Where(r => r.Status == status)
            .Take(limit)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<FileQuarantineRecord>>(results);
    }

    private static string CompositeKey(string key, string provider) => $"{key}|{provider}";
}
