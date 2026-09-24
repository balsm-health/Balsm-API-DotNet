using Balsm.Auth.Application.Commands;
using Balsm.SharedKernel.Contracts;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

/// Verifies a magic sign-in link and issues a session. Selects the challenge by
/// link-token hash, then runs the SAME registration-only account creation and
/// access/refresh issuance as <see cref="VerifyOtpHandler"/> — behavior is kept
/// identical (new user created; existing identity rejected) so both entry points
/// converge.
public sealed class VerifyLinkHandler(
    AuthDbContext authDb,
    IUserAccountProvisioner accountProvisioner,
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

        var existing = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct);

        // Signs in or creates, mirroring VerifyOtpHandler: the link proves the
        // mailbox exactly as the code does, and refusing an existing identity
        // here would answer the question the merged flow hides.
        var isNewUser = existing is null;
        Guid userId;
        if (existing is not null)
        {
            userId = existing.UserId;
        }
        else
        {
            userId = await accountProvisioner.ProvisionAsync("EG", "ar-EG", ct);
            var identity = UserIdentity.Create(userId, "email", email, email);
            identity.ConfirmEmail(DateTime.UtcNow);
            authDb.UserIdentities.Add(identity);
        }

        var accessToken = jwt.IssueAccessToken(userId, email);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();

        var refreshToken = UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30));
        authDb.UserRefreshTokens.Add(refreshToken);
        await authDb.SaveChangesAsync(ct);

        return new AuthTokenResult(accessToken, refreshRaw, userId, IsNewUser: isNewUser);
    }
}
