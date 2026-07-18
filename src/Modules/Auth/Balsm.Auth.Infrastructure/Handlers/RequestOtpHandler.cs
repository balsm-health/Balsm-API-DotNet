using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
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

        var (code, hash, expiresAt) = otpService.Generate();

        // Persist the challenge (hash only — never the code). Supersede any
        // prior unconsumed challenge for this email so only the newest is valid.
        var priorChallenges = await db.OtpChallenges
            .Where(c => c.EmailNormalized == email && c.ConsumedAt == null)
            .ToListAsync(ct);
        foreach (var prior in priorChallenges) prior.Consume();
        db.OtpChallenges.Add(OtpChallenge.Create(email, hash, expiresAt));
        await db.SaveChangesAsync(ct);

        await otpService.SendAsync(email, code, "en", ct);

        logger.LogInformation("OTP issued for [email]");
        return new RequestOtpResult(600);
    }
}
