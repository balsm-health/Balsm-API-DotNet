using Balsm.EmergencyQr.Application.Commands;
using Balsm.EmergencyQr.Domain;
using Balsm.EmergencyQr.Infrastructure.Data;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.EmergencyQr.Infrastructure.Handlers;

public sealed class RevokeEmergencyQrHandler(EmergencyQrDbContext db)
    : IRequestHandler<RevokeEmergencyQrCommand, Result>
{
    public async Task<Result> Handle(RevokeEmergencyQrCommand cmd, CancellationToken ct)
    {
        var token = await db.EmergencyQrTokens.FindAsync([cmd.TokenId], ct);
        // Unknown and not-owned collapse into one answer (contract: 404) so
        // ownership cannot be probed.
        if (token is null || token.UserId != cmd.RequestingUserId)
            return Result.Failure(EmergencyQrErrors.NotFound);
        token.Revoke();
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
