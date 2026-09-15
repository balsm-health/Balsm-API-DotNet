using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class PasswordSignInHandler(
    AuthDbContext authDb,
    PasswordHasher hasher,
    JwtService jwt) : IRequestHandler<PasswordSignInCommand, Result<AuthTokenResult>>
{
    public async Task<Result<AuthTokenResult>> Handle(PasswordSignInCommand cmd, CancellationToken ct)
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
            // Expected failure — a Result, not an exception (shared standards §1):
            // a wrong password is a business outcome, not a fault.
            return Result.Failure<AuthTokenResult>(AuthErrors.InvalidCredentials);
        }

        var userId = identity.UserId;
        var accessToken = jwt.IssueAccessToken(userId, email);
        var (refreshRaw, refreshHash) = jwt.IssueRefreshToken();

        authDb.UserRefreshTokens.Add(
            UserRefreshToken.Create(userId, refreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30)));
        await authDb.SaveChangesAsync(ct);

        return Result.Success(new AuthTokenResult(accessToken, refreshRaw, userId, IsNewUser: false));
    }
}
