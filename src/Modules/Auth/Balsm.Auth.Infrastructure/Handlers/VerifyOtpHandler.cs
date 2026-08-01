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

        var existing = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct);

        // OTP verify completes registration only. An email that already has an
        // identity signs in with a password or Google/Apple — email-OTP login was
        // removed to conserve email quota.
        if (existing is not null)
            throw new AccountAlreadyExistsException(email);

        var account = UserAccount.Create(countryCode: "EG", preferredLanguage: "ar-EG");
        accountDb.UserAccounts.Add(account);
        await accountDb.SaveChangesAsync(ct);
        var userId = account.Id;

        var identity = UserIdentity.Create(userId, "email", email, email);
        identity.ConfirmEmail(DateTime.UtcNow);
        authDb.UserIdentities.Add(identity);

        var accessToken = jwt.IssueAccessToken(userId, email);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();

        var refreshToken = UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30));
        authDb.UserRefreshTokens.Add(refreshToken);
        await authDb.SaveChangesAsync(ct);
        await accountDb.SaveChangesAsync(ct);

        return new AuthTokenResult(accessToken, refreshRaw, userId, IsNewUser: true);
    }
}
