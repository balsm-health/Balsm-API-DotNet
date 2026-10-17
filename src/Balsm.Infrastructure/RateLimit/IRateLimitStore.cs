namespace Balsm.Infrastructure.RateLimit;

/// <summary>
/// Fixed-window rate-limit counter store. Implementations must make consume atomic:
/// concurrent calls across threads (and, for distributed stores, across replicas)
/// must never allow more than <c>limit</c> consumptions per window.
/// Selected at composition time — InMemory (Standalone) or Redis (Cloud).
/// </summary>
public interface IRateLimitStore
{
    ValueTask<RateLimitDecision> TryConsumeAsync(string key, int limit, TimeSpan window, CancellationToken ct = default);
}

public readonly record struct RateLimitDecision(bool Allowed, int RetryAfterSeconds);
