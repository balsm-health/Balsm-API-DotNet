using Balsm.EmergencyQr.Application.Commands;
using Balsm.EmergencyQr.Domain;
using Balsm.EmergencyQr.Domain.Entities;
using Balsm.EmergencyQr.Infrastructure.Data;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.EmergencyQr.Infrastructure.Handlers;

public sealed class MintEmergencyQrHandler(EmergencyQrDbContext db)
    : IRequestHandler<MintEmergencyQrCommand, Result<MintEmergencyQrResult>>
{
    public async Task<Result<MintEmergencyQrResult>> Handle(MintEmergencyQrCommand cmd, CancellationToken ct)
    {
        // Expected failures are Results (shared standards §1); exceptions stay
        // for faults. TTL is validated here so the domain factory's guard is a
        // last line, not the API contract.
        if (!EmergencyQrToken.AllowedTtlSeconds.Contains(cmd.TtlSeconds))
            return Result.Failure<MintEmergencyQrResult>(EmergencyQrErrors.InvalidTtl);

        // Offline-first idempotency: a client that minted locally retries the
        // same token_id until a sync lands. A repeat of an existing active
        // token refreshes its ciphertext instead of failing on the PK.
        if (cmd.TokenId is { } jti)
        {
            var existing = await db.EmergencyQrTokens.FindAsync([jti], ct);
            if (existing is not null)
            {
                // Another user's token_id: same answer as unknown, so the
                // idempotency path cannot probe ownership.
                if (existing.UserId != cmd.UserId)
                    return Result.Failure<MintEmergencyQrResult>(EmergencyQrErrors.NotFound);
                existing.UpdateCiphertext(cmd.Ciphertext, cmd.ProfileEtag);
                await db.SaveChangesAsync(ct);
                return Result.Success(new MintEmergencyQrResult(existing.Id, existing.ExpiresAt));
            }
        }

        // Revoke any prior active tokens for this user
        var active = await db.EmergencyQrTokens
            .Where(t => t.UserId == cmd.UserId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in active) t.Revoke();

        var token = EmergencyQrToken.Mint(cmd.UserId, cmd.Ciphertext, cmd.ProfileEtag, cmd.TtlSeconds, cmd.TokenId);
        db.EmergencyQrTokens.Add(token);
        await db.SaveChangesAsync(ct);

        return Result.Success(new MintEmergencyQrResult(token.Id, token.ExpiresAt));
    }
}
