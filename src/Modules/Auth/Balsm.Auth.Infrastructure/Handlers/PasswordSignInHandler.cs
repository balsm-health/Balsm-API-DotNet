using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class PasswordSignInHandler(
    AuthDbContext authDb,
    PasswordHasher hasher,
    JwtService jwt) : IRequestHandler<PasswordSignInCommand, AuthTokenResult>
{
    public async Task<AuthTokenResult> Handle(PasswordSignInCommand cmd, CancellationToken ct)
    {
        var email = cmd.Email.Trim().ToLowerInvariant();

        var identity = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.EmailNormalized == email, ct);

        // Uniform failure — never reveal whether the account exists or the
        // password is wrong. Verify runs in constant time.
        if (identity is null ||
            !identity.HasPassword ||
            !hasher.Verify(cmd.Password, identity.PasswordHash!))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var userId = identity.UserId;
        var accessToken = jwt.IssueAccessToken(userId, email);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();

        authDb.UserRefreshTokens.Add(
            UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30)));
        await authDb.SaveChangesAsync(ct);

        return new AuthTokenResult(accessToken, refreshRaw, userId, IsNewUser: false);
    }
}
