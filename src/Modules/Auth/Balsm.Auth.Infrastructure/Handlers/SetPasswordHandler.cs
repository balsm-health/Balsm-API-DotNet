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
        await authDb.SaveChangesAsync(ct);
    }
}
