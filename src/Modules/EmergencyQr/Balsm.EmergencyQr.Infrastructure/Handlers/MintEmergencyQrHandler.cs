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
        // Offline-first idempotency: a client that minted locally retries the
        // same token_id until a sync lands. A repeat of an existing active
        // token refreshes its ciphertext instead of failing on the PK.
        if (cmd.TokenId is { } jti)
        {
            var existing = await db.EmergencyQrTokens.FindAsync([jti], ct);
            if (existing is not null)
            {
                if (existing.UserId != cmd.UserId)
                    throw new UnauthorizedAccessException("Token belongs to different user");
                existing.UpdateCiphertext(cmd.Ciphertext, cmd.ProfileEtag, cmd.PreferredLanguage);
                await db.SaveChangesAsync(ct);
                return new MintEmergencyQrResult(existing.Id, existing.ExpiresAt);
            }
        }

        // Revoke any prior active tokens for this user
        var active = await db.EmergencyQrTokens
            .Where(t => t.UserId == cmd.UserId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active) t.Revoke();

        var token = EmergencyQrToken.Mint(cmd.UserId, cmd.Ciphertext, cmd.ProfileEtag, cmd.PreferredLanguage, cmd.TtlSeconds, cmd.TokenId);
        db.EmergencyQrTokens.Add(token);
        await db.SaveChangesAsync(ct);

        return new MintEmergencyQrResult(token.Id, token.ExpiresAt);
    }
}
