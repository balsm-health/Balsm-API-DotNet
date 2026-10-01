using Balsm.Auth.Application.Commands;
using Balsm.SharedKernel.Contracts;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class VerifyOtpHandler(
    AuthDbContext authDb,
    IUserAccountProvisioner accountProvisioner,
    JwtService jwt,
    OtpService otpService,
    DevOtpCodePolicy devCode) : IRequestHandler<VerifyOtpCommand, AuthTokenResult>
{
    public async Task<AuthTokenResult> Handle(VerifyOtpCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();

        // The newest unconsumed challenge for this address: hash-compared,
        // expiry-checked, single-use.
        var challenge = await authDb.OtpChallenges
            .Where(c => c.EmailNormalized == email && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // The fixed staging code stands in for the delivered one — but only
        // against a challenge that exists and is live, so it short-circuits the
        // mailbox, not the flow. See DevOtpCodePolicy for the other two fences.
        var accepted = challenge is not null &&
            challenge.IsRedeemable &&
            (otpService.Verify(cmd.Code, challenge.CodeHash) || devCode.Accepts(email, cmd.Code));

        if (!accepted) throw new UnauthorizedAccessException("Invalid or expired verification code.");

        challenge!.Consume();

        var existing = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct);

        // An address that already has an identity signs in. Refusing here used to
        // be the point — registration only — but under one merged entry the
        // client cannot know which case it is in, and saying so is the leak the
        // flow exists to close. The code proves the mailbox either way.
        //
        // What it does NOT do is touch the password: verifying a code is not an
        // intent to change credentials, and a typo at the password field must
        // not cost someone the password they actually have.
        var isNewUser = existing is null;
        Guid userId;
        if (existing is not null)
        {
            userId = existing.UserId;
        }
        else
        {
            // The provisioner only sees account rows. An id that any identity
            // still points at is taken too: account deletion purges the
            // account row but leaves the identities, and adopting such an id
            // would attach this new account to a deleted user's identities.
            var proposed = cmd.ClientAccountId;
            if (proposed is { } p && await authDb.UserIdentities.AnyAsync(i => i.UserId == p, ct))
                proposed = null;
            userId = proposed is { } free
                ? await accountProvisioner.ProvisionWithPreferredIdAsync("EG", "ar-EG", free, ct)
                : await accountProvisioner.ProvisionAsync("EG", "ar-EG", ct);
            var identity = UserIdentity.Create(userId, "email", email, email);
            identity.ConfirmEmail(DateTime.UtcNow);
            authDb.UserIdentities.Add(identity);
        }

        var accessToken = jwt.IssueAccessToken(userId, email);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();

        var refreshToken = UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30));
        authDb.UserRefreshTokens.Add(refreshToken);
        await authDb.SaveChangesAsync(ct);

        var adopted = isNewUser && cmd.ClientAccountId is { } asked && asked == userId;
        return new AuthTokenResult(accessToken, refreshRaw, userId, IsNewUser: isNewUser, AdoptedClientId: adopted);
    }
}
