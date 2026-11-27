using Balsm.Infrastructure.RateLimit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Auth;

/// <summary>
/// Decides whether sign-up may answer "this email already has an account"
/// instead of emailing a code.
///
/// Normally it may not: the code goes out whether or not the address is known,
/// so no one can learn who uses Balsm. That costs one email per attempt, and a
/// script that signs up known addresses spends the email quota and floods
/// their owners. Strict mode trades the first protection for the second, for
/// a while, and switches on by itself when emails run high. The app already
/// handles both answers, so no build is needed either way.
///
/// <c>Auth:SignUp:RevealExisting</c>: <c>auto</c> (default), <c>on</c> or <c>off</c>.
/// <c>auto</c> turns strict for <c>Auth:SignUp:StrictMinutes</c> (default 60)
/// after the email provider refuses for quota or the global OTP limit is hit,
/// and while more than <c>Auth:SignUp:StrictAfterEmailsPerHour</c> (default
/// 500) codes went out in the current hour.
/// </summary>
public sealed class SignUpGuard(IRateLimitStore store, IConfiguration configuration, ILogger<SignUpGuard> logger)
{
    private const string SentKey = "otp:sent";
    private const string TripKey = "otp:signup-strict";
    private static readonly TimeSpan SentWindow = TimeSpan.FromHours(1);

    public async ValueTask<bool> IsStrictAsync(CancellationToken ct = default)
    {
        switch (configuration["Auth:SignUp:RevealExisting"]?.Trim().ToLowerInvariant())
        {
            case "on": return true;
            case "off": return false;
        }
        if (await store.CountAsync(TripKey, ct) > 0) return true;
        return await store.CountAsync(SentKey, ct) >= configuration.GetValue("Auth:SignUp:StrictAfterEmailsPerHour", 500);
    }

    /// <summary>Counts one code emailed, for the hourly threshold.</summary>
    public async ValueTask RecordSentAsync(CancellationToken ct = default) =>
        await store.TryConsumeAsync(SentKey, int.MaxValue, SentWindow, ct);

    /// <summary>Turns strict mode on for <c>Auth:SignUp:StrictMinutes</c>.</summary>
    public async ValueTask TripAsync(string reason, CancellationToken ct = default)
    {
        var minutes = Math.Max(1, configuration.GetValue("Auth:SignUp:StrictMinutes", 60));
        await store.TryConsumeAsync(TripKey, int.MaxValue, TimeSpan.FromMinutes(minutes), ct);
        logger.LogWarning("Sign-up now says when an email is already registered ({Reason})", reason);
    }
}
