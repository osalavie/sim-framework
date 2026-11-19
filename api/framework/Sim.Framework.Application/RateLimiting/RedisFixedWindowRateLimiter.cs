using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Sim.Framework.Application.RateLimiting;

/// <summary>
/// Redis-backed fixed-window rate limiter for multi-instance deployments.
/// Uses atomic INCR + EXPIRE via Lua script to ensure correct counting across instances.
/// Fails open (permits request) on Redis connectivity errors.
/// </summary>
public sealed class RedisFixedWindowRateLimiter : RateLimiter
{
    private readonly IDatabase _database;
    private readonly RedisKey _key;
    private readonly int _permitLimit;
    private readonly int _windowSeconds;
    private readonly ILogger? _logger;
    private int _consecutiveFailures;
    private const int MaxConsecutiveFailures = 5;

    // KEYS[1] = rate limit key, ARGV[1] = window seconds
    private const string Script = @"
local current = redis.call('INCR', KEYS[1])
if current == 1 then
    redis.call('EXPIRE', KEYS[1], ARGV[1])
end
return {current, redis.call('TTL', KEYS[1])}";

    public RedisFixedWindowRateLimiter(
        IConnectionMultiplexer redis,
        string instanceName,
        string partitionKey,
        int permitLimit,
        int windowSeconds,
        ILogger? logger = null)
    {
        _database = redis.GetDatabase();
        _key = $"{instanceName}ratelimit:{partitionKey}";
        _permitLimit = permitLimit;
        _windowSeconds = windowSeconds;
        _logger = logger;
    }

    public override RateLimiterStatistics? GetStatistics() => null;

    public override TimeSpan? IdleDuration => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        try
        {
            var result = _database.ScriptEvaluate(
                Script,
                new RedisKey[] { _key },
                new RedisValue[] { _windowSeconds });
            return ParseResult(result);
        }
        catch (Exception ex)
        {
            return FailOpen(ex);
        }
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _database.ScriptEvaluateAsync(
                Script,
                new RedisKey[] { _key },
                new RedisValue[] { _windowSeconds });
            return ParseResult(result);
        }
        catch (Exception ex)
        {
            return FailOpen(ex);
        }
    }

    private RateLimitLease ParseResult(RedisResult result)
    {
        _consecutiveFailures = 0;
        var arr = (RedisResult[])result!;
        var count = (long)arr[0];
        var ttl = (long)arr[1];

        if (count > _permitLimit)
        {
            var retryAfter = TimeSpan.FromSeconds(Math.Max(1, ttl));
            return RedisRateLimitLease.Failed(retryAfter);
        }

        return RedisRateLimitLease.Succeeded();
    }

    private RateLimitLease FailOpen(Exception ex)
    {
        _consecutiveFailures++;
        if (_consecutiveFailures <= MaxConsecutiveFailures || _consecutiveFailures % 100 == 0)
        {
            _logger?.LogWarning(ex,
                "Redis rate limiter failed (consecutive: {Count}), failing open for partition {Key}",
                _consecutiveFailures, _key.ToString());
        }

        return RedisRateLimitLease.Succeeded();
    }

    protected override void Dispose(bool disposing)
    {
        // Nothing to dispose - IDatabase is owned by IConnectionMultiplexer singleton
    }
}

internal sealed class RedisRateLimitLease : RateLimitLease
{
    private readonly bool _acquired;
    private readonly TimeSpan? _retryAfter;

    private RedisRateLimitLease(bool acquired, TimeSpan? retryAfter)
    {
        _acquired = acquired;
        _retryAfter = retryAfter;
    }

    public static RedisRateLimitLease Succeeded() => new(true, null);
    public static RedisRateLimitLease Failed(TimeSpan retryAfter) => new(false, retryAfter);

    public override bool IsAcquired => _acquired;

    public override IEnumerable<string> MetadataNames =>
        _retryAfter.HasValue ? [MetadataName.RetryAfter.Name] : [];

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (metadataName == MetadataName.RetryAfter.Name && _retryAfter.HasValue)
        {
            metadata = _retryAfter.Value;
            return true;
        }

        metadata = null;
        return false;
    }
}
