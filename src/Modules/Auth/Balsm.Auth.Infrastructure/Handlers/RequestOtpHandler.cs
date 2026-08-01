using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.RateLimit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class RequestOtpHandler(
    AuthDbContext db,
    OtpService otpService,
    OtpRateLimitPolicies rateLimits,
    IConfiguration configuration,
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

        // OTP emails are spent only on registration and password-reset — email-OTP
        // login was removed to conserve email quota. Branch on whether this email
        // already has an identity so a login attempt never sends an email.
        var identityExists = await db.UserIdentities
            .AnyAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct);
        switch (cmd.Purpose)
        {
            case OtpPurpose.Register when identityExists:
                throw new EmailAlreadyRegisteredException(email);
            case OtpPurpose.Reset when !identityExists:
                // Anti-enumeration: report the same success shape but send nothing,
                // so a caller cannot probe which emails are registered.
                logger.LogInformation("OTP reset requested for unknown [email]; no email sent");
                return new RequestOtpResult(600);
        }

        var (code, hash, linkToken, linkTokenHash, expiresAt) = otpService.Generate();

        // Persist the challenge (hashes only — never the raw code or link token).
        // Supersede any prior unconsumed challenge for this email so only the
        // newest is valid.
        var priorChallenges = await db.OtpChallenges
            .Where(c => c.EmailNormalized == email && c.ConsumedAt == null)
            .ToListAsync(ct);
        foreach (var prior in priorChallenges) prior.Consume();
        db.OtpChallenges.Add(OtpChallenge.Create(email, hash, expiresAt, linkTokenHash));
        await db.SaveChangesAsync(ct);

        // Magic sign-in link: an https URL that 302-bounces the browser into the
        // app's custom scheme carrying the raw token (see AuthController.GetOtpLink).
        var linkBase = configuration["Otp:LinkBaseUrl"] ?? "http://localhost:5000";
        var linkUrl = $"{linkBase.TrimEnd('/')}/auth/otp/link?t={Uri.EscapeDataString(linkToken)}";
        await otpService.SendAsync(email, code, linkUrl, "en", ct);

        logger.LogInformation("OTP issued for [email]");
        return new RequestOtpResult(600);
    }
}
