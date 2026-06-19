using Balsm.EmergencyQr.Application.Commands;
using Balsm.EmergencyQr.Domain.Entities;
using Balsm.EmergencyQr.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.EmergencyQr.Infrastructure.Handlers;

public sealed class MintEmergencyQrHandler(EmergencyQrDbContext db)
    : IRequestHandler<MintEmergencyQrCommand, MintEmergencyQrResult>
{
    public async Task<MintEmergencyQrResult> Handle(MintEmergencyQrCommand cmd, CancellationToken ct)
    {
        // Revoke any prior active tokens for this user
        var active = await db.EmergencyQrTokens
            .Where(t => t.UserId == cmd.UserId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active) t.Revoke();

        var token = EmergencyQrToken.Mint(cmd.UserId, cmd.Ciphertext, cmd.ProfileEtag, cmd.PreferredLanguage, cmd.TtlSeconds);
        db.EmergencyQrTokens.Add(token);
        await db.SaveChangesAsync(ct);

        return new MintEmergencyQrResult(token.Id, token.ExpiresAt);
    }
}
