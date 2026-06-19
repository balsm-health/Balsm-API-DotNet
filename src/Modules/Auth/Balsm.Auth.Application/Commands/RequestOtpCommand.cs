using MediatR;

namespace Balsm.Auth.Application.Commands;

public sealed record RequestOtpCommand(
    string Email,
    string CountryCode,
    string? CaptchaToken,
    string? ClientIp = null) : IRequest<RequestOtpResult>;
public sealed record RequestOtpResult(int ExpiresInSeconds);
public sealed class AccountLockedException(DateTime lockedUntil) : Exception($"Account locked until {lockedUntil:O}")
{
    public DateTime LockedUntil { get; } = lockedUntil;
}
public sealed class OtpRateLimitException(int retryAfterSeconds, string tier)
    : Exception($"OTP rate limit exceeded ({tier}) — retry after {retryAfterSeconds}s")
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
    public string Tier { get; } = tier;
}
