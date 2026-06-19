using Balsm.Geofence.Domain;
using Balsm.Geofence.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Geofence.Infrastructure.Services;

public sealed class GeofenceService(GeofenceDbContext db) : IGeofenceService
{
    public async Task<bool> IsDeniedAsync(string countryCode, CancellationToken ct = default)
    {
        var code = countryCode.ToUpperInvariant();
        return await db.DeniedCountries
            .AsNoTracking()
            .AnyAsync(r => r.CountryCode == code, ct);
    }
}
