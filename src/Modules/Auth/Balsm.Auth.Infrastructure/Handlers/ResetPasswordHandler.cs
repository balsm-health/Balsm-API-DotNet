using Balsm.Auth.Application.Commands;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Balsm.Auth.Infrastructure.Handlers;

/// Resets a forgotten password. The reset token is the emailed OTP code —
/// "forgot password" reuses POST /auth/otp/request to send it — verified the
/// same way OTP sign-in verifies (Otp:DevCode override + stored challenge).
public sealed class ResetPasswordHandler(
    AuthDbContext authDb,
    PasswordHasher hasher,
    OtpService otpService,
    IConfiguration configuration) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        if (cmd.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        var email = cmd.Email.Trim().ToLowerInvariant();

        var devCode = configuration["Otp:DevCode"];
        var devMatch = !string.IsNullOrEmpty(devCode) &&
            string.Equals(cmd.Code, devCode, StringComparison.Ordinal);

        if (!devMatch)
        {
            var challenge = await authDb.OtpChallenges
                .Where(c => c.EmailNormalized == email && c.ConsumedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (challenge is null ||
                !challenge.IsRedeemable ||
                !otpService.Verify(cmd.Code, challenge.CodeHash))
            {
                throw new UnauthorizedAccessException("Invalid or expired reset code.");
            }

            challenge.Consume();
        }

        var identity = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct)
            ?? throw new KeyNotFoundException("Email identity not found.");

        identity.SetPasswordHash(hasher.Hash(cmd.NewPassword));
        await authDb.SaveChangesAsync(ct);
    }
}
