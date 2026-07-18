using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class VerifyOtpHandler(
    AuthDbContext authDb,
    AccountDbContext accountDb,
    JwtService jwt,
    OtpService otpService,
    IConfiguration configuration) : IRequestHandler<VerifyOtpCommand, AuthTokenResult>
{
    public async Task<AuthTokenResult> Handle(VerifyOtpCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();

        // Fixed dev/test OTP override: when `Otp:DevCode` is configured, that
        // code is always accepted (deterministic local/staging E2E without email
        // delivery). Set it ONLY in non-production config. Any other code falls
        // through to real challenge verification below.
        var devCode = configuration["Otp:DevCode"];
        var devMatch = !string.IsNullOrEmpty(devCode) &&
            string.Equals(cmd.Code, devCode, StringComparison.Ordinal);

        if (!devMatch)
        {
            // Verify against the stored challenge: newest unconsumed entry for
            // this email, hash-compared, expiry-checked, single-use.
            var challenge = await authDb.OtpChallenges
                .Where(c => c.EmailNormalized == email && c.ConsumedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (challenge is null ||
                !challenge.IsRedeemable ||
                !otpService.Verify(cmd.Code, challenge.CodeHash))
            {
                throw new UnauthorizedAccessException("Invalid or expired verification code.");
            }

            challenge.Consume();
        }

        var identity = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct);

        bool isNew = identity is null;
        Guid userId;

        if (isNew)
        {
            var account = UserAccount.Create(countryCode: "EG", preferredLanguage: "ar-EG");
            accountDb.UserAccounts.Add(account);
            await accountDb.SaveChangesAsync(ct);
            userId = account.Id;

            identity = UserIdentity.Create(userId, "email", email, email);
            identity.ConfirmEmail(DateTime.UtcNow);
            authDb.UserIdentities.Add(identity);
        }
        else
        {
            userId = identity!.UserId;
        }

        var accessToken = jwt.IssueAccessToken(userId, email);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();

        var refreshToken = UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30));
        authDb.UserRefreshTokens.Add(refreshToken);
        await authDb.SaveChangesAsync(ct);
        await accountDb.SaveChangesAsync(ct);

        return new AuthTokenResult(accessToken, refreshRaw, userId, isNew);
    }
}
