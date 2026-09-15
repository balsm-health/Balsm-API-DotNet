using Balsm.EmergencyQr.Application.Commands;
using Balsm.EmergencyQr.Domain;
using Balsm.EmergencyQr.Infrastructure.Data;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.EmergencyQr.Infrastructure.Handlers;

public sealed class UpdateEmergencyQrCiphertextHandler(EmergencyQrDbContext db)
    : IRequestHandler<UpdateEmergencyQrCiphertextCommand, Result>
{
    public async Task<Result> Handle(UpdateEmergencyQrCiphertextCommand cmd, CancellationToken ct)
    {
        var token = await db.EmergencyQrTokens.FindAsync([cmd.TokenId], ct);
        // Unknown and not-owned collapse into one answer (contract: 404) so
        // ownership cannot be probed.
        if (token is null || token.UserId != cmd.RequestingUserId)
            return Result.Failure(EmergencyQrErrors.NotFound);
        if (!token.IsActive)
            return Result.Failure(EmergencyQrErrors.TokenInactive);
        token.UpdateCiphertext(cmd.Ciphertext, cmd.ProfileEtag);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
