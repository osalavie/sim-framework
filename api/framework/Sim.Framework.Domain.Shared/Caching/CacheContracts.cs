namespace Sim.Framework.Domain.Shared.Caching;

public sealed class CacheEntryOptions
{
    public TimeSpan Expiration { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan? LocalExpiration { get; init; }
    public bool CacheNull { get; init; }
}

public interface ICache
{
    Task<T?> GetAsync<T>(string region, string key, CancellationToken cancellationToken = default);

    Task SetAsync<T>(
        string region,
        string key,
        T value,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string region, string key, CancellationToken cancellationToken = default);

    Task<T?> GetOrCreateAsync<T>(
        string region,
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default);
}
