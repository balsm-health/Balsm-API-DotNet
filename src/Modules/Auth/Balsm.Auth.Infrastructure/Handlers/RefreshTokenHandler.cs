using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain.Entities;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class RefreshTokenHandler(
    AuthDbContext db,
    JwtService jwt) : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var hash = jwt.HashRefreshToken(cmd.RefreshToken);
        var token = await db.UserRefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.DeviceId == cmd.DeviceId, ct)
            ?? throw new InvalidOperationException("TokenNotFound");

        if (!token.IsActive) throw new InvalidOperationException("TokenRevoked");

        token.Revoke();
        var identity = await db.UserIdentities.FirstOrDefaultAsync(i => i.UserId == token.UserId, ct);
        var email = identity?.EmailNormalized ?? string.Empty;

        var newAccess = jwt.IssueAccessToken(token.UserId, email);
        var (newRefreshRaw, newRefreshHash) = jwt.IssueRefreshToken();
        var newToken = UserRefreshToken.Create(token.UserId, newRefreshHash, cmd.DeviceId, DateTime.UtcNow.AddDays(30));

        db.UserRefreshTokens.Add(newToken);
        await db.SaveChangesAsync(ct);
        return new RefreshTokenResult(newAccess, newRefreshRaw);
    }
}
