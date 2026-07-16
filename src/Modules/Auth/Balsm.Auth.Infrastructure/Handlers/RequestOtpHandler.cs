using Balsm.Auth.Application.Commands;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.RateLimit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class RequestOtpHandler(
    AuthDbContext db,
    OtpService otpService,
    OtpRateLimitPolicies rateLimits,
    ILogger<RequestOtpHandler> logger) : IRequestHandler<RequestOtpCommand, RequestOtpResult>
{
    public async Task<RequestOtpResult> Handle(RequestOtpCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();
        var ip = cmd.ClientIp ?? "unknown";

        // Rate-limit checks (FR-045a/b)
        var emailDecision = await rateLimits.CheckEmailAsync(email, ct);
        if (!emailDecision.Allowed)
            throw new OtpRateLimitException(emailDecision.RetryAfterSeconds, "email");

        var ipDecision = await rateLimits.CheckIpAsync(ip, ct);
        if (!ipDecision.Allowed)
            throw new OtpRateLimitException(ipDecision.RetryAfterSeconds, "ip");

        var globalDecision = await rateLimits.CheckGlobalAsync(ct);
        if (!globalDecision.Allowed)
            throw new OtpRateLimitException(globalDecision.RetryAfterSeconds, "global");

        var lockout = await db.AccountLockouts
            .FirstOrDefaultAsync(l => l.Identifier == email && l.IdentifierType == "email", ct);
        if (lockout?.IsLocked == true)
            throw new AccountLockedException(lockout.LockedUntil!.Value);

        var (code, _, _) = otpService.Generate();
        await otpService.SendAsync(email, code, "en", ct);

        logger.LogInformation("OTP issued for [email]");
        return new RequestOtpResult(600);
    }
}
