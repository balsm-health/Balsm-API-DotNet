using Balsm.EmergencyQr.Domain.Entities;
using Balsm.EmergencyQr.Infrastructure.Data;
using Balsm.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balsm.EmergencyQr.Infrastructure.Services;

/// <summary>
/// EmergencyQr-side implementation of the jti → owner contract used by family
/// linking. Read-only: unlike the public resolve it records no scan, and it
/// answers <c>null</c> for unknown, revoked and expired tokens alike.
/// </summary>
public sealed class ProfileQrOwnerResolver(EmergencyQrDbContext db, TimeProvider clock) : IProfileQrOwnerResolver
{
    public async Task<Guid?> ResolveOwnerAsync(Guid jti, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return await db.EmergencyQrTokens.AsNoTracking()
            .Where(t => t.Id == jti
                        && t.Type == EmergencyQrToken.ProfileType
                        && t.RevokedAt == null
                        && (t.ExpiresAt == null || t.ExpiresAt > now))
            .Select(t => (Guid?)t.UserId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
