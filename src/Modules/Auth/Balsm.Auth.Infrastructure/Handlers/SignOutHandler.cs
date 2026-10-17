using Balsm.Auth.Application.Commands;
using Balsm.Auth.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Auth.Infrastructure.Handlers;

public sealed class SignOutHandler(AuthDbContext db) : IRequestHandler<SignOutCommand>
{
    public async Task Handle(SignOutCommand cmd, CancellationToken ct)
    {
        var tokens = await db.UserRefreshTokens
            .Where(t => t.UserId == cmd.UserId && t.DeviceId == cmd.DeviceId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in tokens) t.Revoke();
        await db.SaveChangesAsync(ct);
    }
}
