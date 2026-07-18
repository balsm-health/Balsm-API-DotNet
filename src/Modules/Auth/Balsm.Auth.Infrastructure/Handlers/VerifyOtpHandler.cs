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
    IConfiguration configuration) : IRequestHandler<VerifyOtpCommand, AuthTokenResult>
{
    public async Task<AuthTokenResult> Handle(VerifyOtpCommand cmd, CancellationToken ct)
    {
        // Fixed dev/test OTP override. When `Otp:DevCode` is configured, that
        // code is the ONLY accepted OTP — any other code is rejected (401).
        // This gives deterministic sign-in for local/staging E2E without email
        // delivery. Set it ONLY in non-production config; leaving it unset
        // disables the check. (Real OTP-challenge verification against a stored
        // hash is not yet implemented — RequestOtpHandler does not persist the
        // code — and is tracked as a separate security fix.)
        var devCode = configuration["Otp:DevCode"];
        if (!string.IsNullOrEmpty(devCode) &&
            !string.Equals(cmd.Code, devCode, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Invalid verification code.");
        }

        var email = cmd.Email.Trim().ToLowerInvariant();

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
