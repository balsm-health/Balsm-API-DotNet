using Balsm.EmergencyQr.Application.Queries;
using Balsm.EmergencyQr.Domain.Entities;
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

        // Scan history (spec v2.0): successful resolves only, no scanner identity.
        db.QrScanRecords.Add(QrScanRecord.Record(token.Id, token.UserId, query.ClientClass));
        token.RecordScan(query.ClientClass);
        await db.SaveChangesAsync(ct);

        return new ResolveEmergencyQrResult(token.Ciphertext, token.Type, token.ExpiresAt);
    }
}
