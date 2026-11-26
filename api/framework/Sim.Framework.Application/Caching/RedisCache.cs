using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sim.Framework.Domain;
using Sim.Framework.Domain.Shared.Caching;
using StackExchange.Redis;

namespace Sim.Framework.Application.Caching;

/// <summary>
/// Redis cache adapter backed by the shared StackExchange.Redis connection multiplexer.
/// Redis-specific types remain internal to the framework implementation.
/// </summary>
public sealed class RedisCache(
    IConnectionMultiplexer connection,
    IOptions<CachingOptions> options) : ICache
{
    private sealed record CacheValue<T>(bool HasValue, T? Value);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private readonly IDatabase _database = connection.GetDatabase();
    private readonly string _prefix = options.Value.InstanceName;

    public async Task<T?> GetAsync<T>(
        string region,
        string key,
        CancellationToken cancellationToken = default)
    {
        var value = await ReadAsync<T>(region, key, cancellationToken);
        return value.Value;
    }

    public async Task SetAsync<T>(
        string region,
        string key,
        T value,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new CacheEntryOptions();
        if (value is null && !options.CacheNull) return;

        var json = JsonSerializer.Serialize(new CacheValue<T>(true, value), SerializerOptions);
        await _database.StringSetAsync(
            BuildKey(region, key),
            json,
            options.Expiration,
            flags: CommandFlags.DemandMaster);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task RemoveAsync(
        string region,
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _database.KeyDeleteAsync(
            BuildKey(region, key),
            CommandFlags.DemandMaster);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string region,
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var cached = await ReadAsync<T>(region, key, cancellationToken);
        if (cached.Found) return cached.Value;

        var normalized = BuildKey(region, key).ToString();
        var gate = _locks.GetOrAdd(normalized, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            cached = await ReadAsync<T>(region, key, cancellationToken);
            if (cached.Found) return cached.Value;

            var created = await factory(cancellationToken);
            await SetAsync(region, key, created, options, cancellationToken);
            return created;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<(bool Found, T? Value)> ReadAsync<T>(
        string region,
        string key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var response = await _database.StringGetAsync(
            BuildKey(region, key),
            CommandFlags.PreferReplica);
        cancellationToken.ThrowIfCancellationRequested();
        if (response.IsNull) return (false, default);

        var value = JsonSerializer.Deserialize<CacheValue<T>>(
            response.ToString(),
            SerializerOptions);
        return value is { HasValue: true } ? (true, value.Value) : (false, default);
    }

    private RedisKey BuildKey(string region, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var tenant = TenantContext.CurrentTenantId?.ToString("N") ?? "host";
        return $"{_prefix}{tenant}:{region.Trim().ToLowerInvariant()}:{key.Trim()}";
    }
}
