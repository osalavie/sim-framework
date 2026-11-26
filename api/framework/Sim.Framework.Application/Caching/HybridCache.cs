using Sim.Framework.Domain.Shared.Caching;

namespace Sim.Framework.Application.Caching;

public sealed class HybridCache(LocalCache local, RedisCache redis) : ICache
{
    public async Task<T?> GetAsync<T>(
        string region,
        string key,
        CancellationToken cancellationToken = default)
    {
        var value = await local.GetAsync<T>(region, key, cancellationToken);
        if (value is not null) return value;
        value = await redis.GetAsync<T>(region, key, cancellationToken);
        if (value is not null)
        {
            await local.SetAsync(
                region,
                key,
                value,
                new CacheEntryOptions { Expiration = TimeSpan.FromSeconds(30) },
                cancellationToken);
        }

        return value;
    }

    public async Task SetAsync<T>(
        string region,
        string key,
        T value,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await redis.SetAsync(region, key, value, options, cancellationToken);
        await local.SetAsync(region, key, value, options, cancellationToken);
    }

    public async Task RemoveAsync(
        string region,
        string key,
        CancellationToken cancellationToken = default)
    {
        await redis.RemoveAsync(region, key, cancellationToken);
        await local.RemoveAsync(region, key, cancellationToken);
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string region,
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync<T>(region, key, cancellationToken);
        if (existing is not null) return existing;
        var created = await redis.GetOrCreateAsync(region, key, factory, options, cancellationToken);
        if (created is not null) await local.SetAsync(region, key, created, options, cancellationToken);
        return created;
    }
}
