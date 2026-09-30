using Balsm.Auth.Application.Commands;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

/// Resets a forgotten password. The reset token is the emailed OTP code —
/// "forgot password" reuses POST /auth/otp/request to send it — verified the
/// same way OTP sign-in verifies (Otp:DevCode override + stored challenge).
public sealed class ResetPasswordHandler(
    AuthDbContext authDb,
    PasswordHasher hasher,
    OtpService otpService,
    DevOtpCodePolicy devCode) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        if (cmd.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        var email = cmd.Email.Trim().ToLowerInvariant();

        var challenge = await authDb.OtpChallenges
            .Where(c => c.EmailNormalized == email && c.ConsumedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // Same three fences as verify. This path previously accepted the fixed
        // code with no environment check at all, which made it a password reset
        // for any account whose address you knew.
        var accepted = challenge is not null &&
            challenge.IsRedeemable &&
            (otpService.Verify(cmd.Code, challenge.CodeHash) || devCode.Accepts(email, cmd.Code));

        if (!accepted) throw new UnauthorizedAccessException("Invalid or expired reset code.");

        challenge!.Consume();

        var identity = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct)
            ?? throw new KeyNotFoundException("Email identity not found.");

        identity.SetPasswordHash(hasher.Hash(cmd.NewPassword));

        // A reset is the one moment a person is most likely to be locking
        // someone else out. Sessions that predate it go with the old password.
        var tokens = await authDb.UserRefreshTokens
            .Where(t => t.UserId == identity.UserId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in tokens) token.Revoke();

        await authDb.SaveChangesAsync(ct);
    }
}
