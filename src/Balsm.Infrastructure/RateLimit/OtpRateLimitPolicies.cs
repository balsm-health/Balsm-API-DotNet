using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.RateLimit;

/// <summary>
/// In-process fixed-window rate limiting for OTP requests. FR-045a/b/c.
/// Uses IMemoryCache — single-host; swap for IDistributedCache if multi-node.
/// </summary>
public sealed class OtpRateLimitPolicies(IMemoryCache cache, ILogger<OtpRateLimitPolicies> logger)
{
    private const int EmailLimit = 3;
    private const int IpLimit = 10;
    private const int GlobalLimit = 10_000;
    private static readonly TimeSpan EmailWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan GlobalWindow = TimeSpan.FromMinutes(60);

    private sealed record Window(int Count, DateTimeOffset Expires);

    private bool TryConsume(string key, int limit, TimeSpan window, out int retryAfterSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var w = cache.Get<Window>(key);
        if (w is null || now >= w.Expires)
            w = new Window(0, now.Add(window));

        if (w.Count >= limit)
        {
            retryAfterSeconds = Math.Max(1, (int)(w.Expires - now).TotalSeconds);
            return false;
        }

        cache.Set(key, w with { Count = w.Count + 1 }, w.Expires);
        retryAfterSeconds = 0;
        return true;
    }

    public bool CheckEmail(string email, out int retryAfterSeconds)
    {
        var allowed = TryConsume($"otp:email:{email.ToLowerInvariant()}", EmailLimit, EmailWindow, out retryAfterSeconds);
        if (!allowed) logger.LogWarning("OTP email rate limit hit for [email]");
        return allowed;
    }

    public bool CheckIp(string ip, out int retryAfterSeconds)
    {
        var allowed = TryConsume($"otp:ip:{ip}", IpLimit, IpWindow, out retryAfterSeconds);
        if (!allowed) logger.LogWarning("OTP IP rate limit hit for [ip]");
        return allowed;
    }

    public bool CheckGlobal(out int retryAfterSeconds)
    {
        var allowed = TryConsume("otp:global", GlobalLimit, GlobalWindow, out retryAfterSeconds);
        if (!allowed) logger.LogError("OTP global rate limit reached — possible attack or misconfiguration");
        return allowed;
    }
}
