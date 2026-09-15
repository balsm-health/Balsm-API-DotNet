using Balsm.EmergencyQr.Application.Commands;
using Balsm.EmergencyQr.Infrastructure.Data;
using MediatR;

namespace Balsm.EmergencyQr.Infrastructure.Handlers;

public sealed class UpdateEmergencyQrCiphertextHandler(EmergencyQrDbContext db)
    : IRequestHandler<UpdateEmergencyQrCiphertextCommand>
{
    public async Task Handle(UpdateEmergencyQrCiphertextCommand cmd, CancellationToken ct)
    {
        var token = await db.EmergencyQrTokens.FindAsync([cmd.TokenId], ct)
            ?? throw new InvalidOperationException("Token not found");
        if (token.UserId != cmd.RequestingUserId)
            throw new UnauthorizedAccessException("Token belongs to different user");
        token.UpdateCiphertext(cmd.Ciphertext, cmd.ProfileEtag);
        await db.SaveChangesAsync(ct);
    }
}
