using Microsoft.Extensions.Caching.Memory;

namespace Balsm.Infrastructure.RateLimit;

/// <summary>
/// In-process fixed-window store for Standalone (single-host) deployments.
/// Interlocked increment keeps the counter atomic under concurrency; the entry
/// evicts itself at window end so a new window starts at zero.
/// </summary>
public sealed class InMemoryRateLimitStore(IMemoryCache cache) : IRateLimitStore
{
    private sealed class Counter
    {
        public int Count;
        public DateTimeOffset Expires;
    }

    public ValueTask<RateLimitDecision> TryConsumeAsync(string key, int limit, TimeSpan window, CancellationToken ct = default)
    {
        var counter = cache.GetOrCreate(key, entry =>
        {
            var expires = DateTimeOffset.UtcNow.Add(window);
            entry.AbsoluteExpiration = expires;
            return new Counter { Expires = expires };
        })!;

        var count = Interlocked.Increment(ref counter.Count);
        if (count > limit)
        {
            var retryAfterSeconds = Math.Max(1, (int)(counter.Expires - DateTimeOffset.UtcNow).TotalSeconds);
            return ValueTask.FromResult(new RateLimitDecision(false, retryAfterSeconds));
        }

        return ValueTask.FromResult(new RateLimitDecision(true, 0));
    }
}
