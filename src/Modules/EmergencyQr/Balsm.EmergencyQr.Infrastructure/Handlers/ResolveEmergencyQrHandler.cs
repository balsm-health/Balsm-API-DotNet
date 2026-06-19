using Balsm.EmergencyQr.Application.Queries;
using Balsm.EmergencyQr.Infrastructure.Data;
using MediatR;

namespace Balsm.EmergencyQr.Infrastructure.Handlers;

public sealed class ResolveEmergencyQrHandler(EmergencyQrDbContext db)
    : IRequestHandler<ResolveEmergencyQrQuery, ResolveEmergencyQrResult?>
{
    public async Task<ResolveEmergencyQrResult?> Handle(ResolveEmergencyQrQuery query, CancellationToken ct)
    {
        var token = await db.EmergencyQrTokens.FindAsync([query.TokenId], ct);
        if (token is null || !token.IsActive) return null;
        return new ResolveEmergencyQrResult(token.Ciphertext, token.PreferredLanguage, token.ExpiresAt);
    }
}
