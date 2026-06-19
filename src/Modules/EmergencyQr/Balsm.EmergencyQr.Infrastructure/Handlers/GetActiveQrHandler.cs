using Balsm.EmergencyQr.Application.Queries;
using Balsm.EmergencyQr.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.EmergencyQr.Infrastructure.Handlers;

public sealed class GetActiveQrHandler(EmergencyQrDbContext db)
    : IRequestHandler<GetActiveQrQuery, GetActiveQrResult?>
{
    public async Task<GetActiveQrResult?> Handle(GetActiveQrQuery query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var token = await db.EmergencyQrTokens
            .AsNoTracking()
            .Where(t => t.UserId == query.UserId && t.RevokedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.ExpiresAt)
            .FirstOrDefaultAsync(ct);

        return token is null ? null : new GetActiveQrResult(token.Id, token.ExpiresAt, token.TtlSeconds);
    }
}
