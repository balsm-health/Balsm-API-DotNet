using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Balsm.Infrastructure.RateLimit;

/// <summary>
/// Redis-backed fixed-window store for Cloud (multi-replica) deployments.
/// One atomic Lua round-trip: INCR, PEXPIRE on first hit in the window, PTTL when over limit —
/// concurrent requests on different replicas can never slip past the limit.
/// Fails open (allows the request, logs Error) when Redis is unreachable: OTP rate limiting
/// is defense-in-depth and account lockout still guards verification; blocking every login
/// on a cache outage is the worse failure for a healthcare app.
/// </summary>
public sealed class RedisRateLimitStore(IConnectionMultiplexer redis, ILogger<RedisRateLimitStore> logger) : IRateLimitStore
{
    // Returns 0 when allowed, remaining window in milliseconds when over the limit.
    private const string Script = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        if count > tonumber(ARGV[2]) then
            return redis.call('PTTL', KEYS[1])
        end
        return 0
        """;

    public async ValueTask<RateLimitDecision> TryConsumeAsync(string key, int limit, TimeSpan window, CancellationToken ct = default)
    {
        try
        {
            var db = redis.GetDatabase();
            var result = (long)await db.ScriptEvaluateAsync(
                Script,
                new RedisKey[] { key },
                new RedisValue[] { (long)window.TotalMilliseconds, limit }).ConfigureAwait(false);

            if (result == 0)
                return new RateLimitDecision(true, 0);

            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(result / 1000.0));
            return new RateLimitDecision(false, retryAfterSeconds);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            logger.LogError(ex, "Redis rate-limit store unavailable — failing open");
            return new RateLimitDecision(true, 0);
        }
    }
}
