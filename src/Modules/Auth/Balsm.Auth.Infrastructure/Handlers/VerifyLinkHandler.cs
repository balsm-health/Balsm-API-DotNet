using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

/// Verifies a magic sign-in link and issues a session. Selects the challenge by
/// link-token hash, then runs the SAME find/create identity + account and
/// access/refresh issuance as <see cref="VerifyOtpHandler"/> — behavior is kept
/// identical (including new-user defaults) so both entry points converge.
public sealed class VerifyLinkHandler(
    AuthDbContext authDb,
    AccountDbContext accountDb,
    JwtService jwt,
    OtpService otpService) : IRequestHandler<VerifyLinkCommand, AuthTokenResult>
{
    public async Task<AuthTokenResult> Handle(VerifyLinkCommand cmd, CancellationToken ct)
    {
        // Hash the incoming raw token and match it against the stored link hash.
        // The raw token never touches the database.
        var hash = otpService.HashToken(cmd.Token);

        var challenge = await authDb.OtpChallenges
            .Where(c => c.ConsumedAt == null && c.LinkTokenHash == hash)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (challenge is null || !challenge.IsRedeemable)
            throw new UnauthorizedAccessException("Invalid or expired sign-in link.");

        challenge.Consume();

        // The challenge carries the email the link was issued for.
        var email = challenge.EmailNormalized;

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
