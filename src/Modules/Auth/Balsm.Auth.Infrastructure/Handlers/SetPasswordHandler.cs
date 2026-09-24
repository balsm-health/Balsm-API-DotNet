using Balsm.Auth.Application.Commands;
using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class SetPasswordHandler(
    AuthDbContext authDb,
    PasswordHasher hasher) : IRequestHandler<SetPasswordCommand>
{
    public async Task Handle(SetPasswordCommand cmd, CancellationToken ct)
    {
        if (cmd.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");

        var identity = await authDb.UserIdentities
            .FirstOrDefaultAsync(i => i.Provider == "email" && i.UserId == cmd.UserId, ct)
            ?? throw new KeyNotFoundException("Email identity not found.");

        identity.SetPasswordHash(hasher.Hash(cmd.Password));

        // Every other session ends with the old password. Someone who changes
        // their password because a session is not theirs expects exactly that,
        // and a refresh token outliving the change would keep the intruder in
        // for the rest of its 30 days.
        var tokens = await authDb.UserRefreshTokens
            .Where(t => t.UserId == cmd.UserId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in tokens) token.Revoke();

        await authDb.SaveChangesAsync(ct);
    }
}
