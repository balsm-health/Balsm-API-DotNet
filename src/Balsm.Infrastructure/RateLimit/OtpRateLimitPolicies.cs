using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.RateLimit;

/// <summary>
/// Fixed-window rate limiting for OTP requests. FR-045a/b/c.
/// Counters live behind <see cref="IRateLimitStore"/> — in-memory in Standalone,
/// Redis (atomic, shared across replicas) in Cloud.
/// </summary>
public sealed class OtpRateLimitPolicies(IRateLimitStore store, ILogger<OtpRateLimitPolicies> logger)
{
    private const int EmailLimit = 3;
    private const int IpLimit = 10;
    private const int GlobalLimit = 10_000;
    private static readonly TimeSpan EmailWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan GlobalWindow = TimeSpan.FromMinutes(60);

    public async ValueTask<RateLimitDecision> CheckEmailAsync(string email, CancellationToken ct = default)
    {
        var decision = await store
            .TryConsumeAsync($"otp:email:{email.ToLowerInvariant()}", EmailLimit, EmailWindow, ct)
            .ConfigureAwait(false);
        if (!decision.Allowed) logger.LogWarning("OTP email rate limit hit for [email]");
        return decision;
    }

    public async ValueTask<RateLimitDecision> CheckIpAsync(string ip, CancellationToken ct = default)
    {
        var decision = await store
            .TryConsumeAsync($"otp:ip:{ip}", IpLimit, IpWindow, ct)
            .ConfigureAwait(false);
        if (!decision.Allowed) logger.LogWarning("OTP IP rate limit hit for [ip]");
        return decision;
    }

    public async ValueTask<RateLimitDecision> CheckGlobalAsync(CancellationToken ct = default)
    {
        var decision = await store
            .TryConsumeAsync("otp:global", GlobalLimit, GlobalWindow, ct)
            .ConfigureAwait(false);
        if (!decision.Allowed) logger.LogError("OTP global rate limit reached — possible attack or misconfiguration");
        return decision;
    }
}
