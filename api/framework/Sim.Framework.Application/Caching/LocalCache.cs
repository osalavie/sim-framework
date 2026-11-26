using System.Collections.Concurrent;
using Sim.Framework.Domain.Shared.Caching;
using Sim.Framework.Domain;

namespace Sim.Framework.Application.Caching;

public sealed class LocalCache : ICache, IDisposable
{
    private sealed record Entry(object? Value, DateTimeOffset ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public Task<T?> GetAsync<T>(
        string region,
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalized = BuildKey(region, key);
        return Task.FromResult(TryGet(normalized, out T? value) ? value : default);
    }

    public Task SetAsync<T>(
        string region,
        string key,
        T value,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new CacheEntryOptions();
        if (value is null && !options.CacheNull)
        {
            return Task.CompletedTask;
        }

        _entries[BuildKey(region, key)] = new Entry(
            value,
            DateTimeOffset.UtcNow.Add(options.LocalExpiration ?? options.Expiration));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(
        string region,
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryRemove(BuildKey(region, key), out _);
        return Task.CompletedTask;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string region,
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = BuildKey(region, key);
        if (TryGet(normalized, out T? existing))
        {
            return existing;
        }

        var gate = _locks.GetOrAdd(normalized, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TryGet(normalized, out existing))
            {
                return existing;
            }

            var created = await factory(cancellationToken);
            await SetAsync(region, key, created, options, cancellationToken);
            return created;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }

        _locks.Clear();
        _entries.Clear();
    }

    private static string BuildKey(string region, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var tenant = TenantContext.CurrentTenantId?.ToString("N") ?? "host";
        return $"{tenant}:{region.Trim().ToLowerInvariant()}:{key.Trim()}";
    }

    private bool TryGet<T>(string normalized, out T? value)
    {
        if (!_entries.TryGetValue(normalized, out var entry))
        {
            value = default;
            return false;
        }

        if (entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _entries.TryRemove(normalized, out _);
            value = default;
            return false;
        }

        value = entry.Value is null ? default : (T?)entry.Value;
        return true;
    }
}
