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
    SignUpGuard signUpGuard,
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
        {
            await signUpGuard.TripAsync("global OTP limit", ct);
            throw new OtpRateLimitException(globalDecision.RetryAfterSeconds, "global");
        }

        var lockout = await db.AccountLockouts
            .FirstOrDefaultAsync(l => l.Identifier == email && l.IdentifierType == "email", ct);
        if (lockout?.IsLocked == true)
            throw new AccountLockedException(lockout.LockedUntil!.Value);

        // Continue never asks whether the account exists: the answer would be
        // visible to any anonymous caller, and "does this person have a Balsm
        // account" is not a question this API answers. Reset still sends nothing
        // for an unknown address, and reports success anyway, for the same reason.
        // So does DeleteAccount: there is no account to delete, and verifying
        // the code would create one.
        if (cmd.Purpose is OtpPurpose.Reset or OtpPurpose.DeleteAccount)
        {
            var identityExists = await db.UserIdentities
                .AnyAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct);
            if (!identityExists)
            {
                logger.LogInformation("OTP {Purpose} requested for unknown [email]; no email sent", cmd.Purpose);
                return new RequestOtpResult(600);
            }
        }

        // Sign-up names a known address only while strict, after the rate
        // limits above, so a prober still gets a handful of answers an hour.
        if (cmd.Purpose == OtpPurpose.SignUp
            && await signUpGuard.IsStrictAsync(ct)
            && await db.UserIdentities.AnyAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct))
        {
            logger.LogInformation("OTP sign-up for a registered [email] refused while strict; no email sent");
            throw new EmailRegisteredException();
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

        // Magic link: an https URL that bounces the browser into the app
        // carrying the raw token (see AuthController.GetOtpLink). `p` names the
        // purpose so the app opens the screen the code was asked from — the
        // password reset, the account deletion — rather than a plain sign-in.
        var linkBase = configuration["Otp:LinkBaseUrl"] ?? "http://localhost:5000";
        var linkUrl = $"{linkBase.TrimEnd('/')}/auth/otp/link?t={Uri.EscapeDataString(linkToken)}";
        if (OtpPurposeWire.LinkCode(cmd.Purpose) is { } linkCode) linkUrl += $"&p={linkCode}";
        var kind = cmd.Purpose switch
        {
            OtpPurpose.Reset => OtpEmailKind.ResetPassword,
            OtpPurpose.DeleteAccount => OtpEmailKind.DeleteAccount,
            _ => OtpEmailKind.SignIn,
        };
        var delivery = await otpService.SendAsync(email, code, linkUrl, cmd.Language ?? "en", kind, ct);
        if (delivery == OtpDelivery.OverQuota) await signUpGuard.TripAsync("email provider quota", ct);
        else if (delivery == OtpDelivery.Sent) await signUpGuard.RecordSentAsync(ct);

        logger.LogInformation("OTP issued for [email]");
        return new RequestOtpResult(600);
    }
}
