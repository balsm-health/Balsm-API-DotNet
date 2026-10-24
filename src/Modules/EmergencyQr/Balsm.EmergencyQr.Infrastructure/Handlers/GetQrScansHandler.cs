using Balsm.EmergencyQr.Application.Queries;
using Balsm.EmergencyQr.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.EmergencyQr.Infrastructure.Handlers;

public sealed class GetQrScansHandler(EmergencyQrDbContext db)
    : IRequestHandler<GetQrScansQuery, IReadOnlyList<QrScanResult>>
{
    public async Task<IReadOnlyList<QrScanResult>> Handle(GetQrScansQuery query, CancellationToken ct)
        => await db.QrScanRecords
            .Where(s => s.OwnerUserId == query.UserId)
            .OrderByDescending(s => s.ResolvedAt)
            .Take(query.Limit)
            .Select(s => new QrScanResult(s.TokenId, s.ResolvedAt, s.ClientClass, s.Country))
            .ToListAsync(ct);
}
