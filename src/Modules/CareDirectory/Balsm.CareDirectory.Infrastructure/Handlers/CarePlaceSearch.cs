using Balsm.CareDirectory.Domain;
using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareDirectory.Infrastructure.Handlers;

/// <summary>
/// The narrowing every care-directory search performs: type filter, bilingual
/// free text, radius cutoff, distance sort, nearest-N.
///
/// Shared rather than duplicated because the Arabic half is easy to get subtly
/// wrong — a handler that compared raw <c>name_ar</c> instead of the normalised
/// column would silently stop matching أشعة against اشعة, and only for that one
/// endpoint. One implementation means the map and the list can never disagree
/// about what a query means.
/// </summary>
internal static class CarePlaceSearch
{
    private const double EarthRadiusKm = 6371.0;

    /// <summary>A matched place and its distance from the query point.</summary>
    internal readonly record struct Hit(CarePlace Place, double DistanceKm);

    internal static async Task<List<Hit>> FindAsync(
        CareDirectoryDbContext db,
        double lat,
        double lng,
        double? radiusKm,
        string? type,
        string? text,
        int limit,
        CancellationToken ct)
    {
        var q = db.CarePlaces.AsNoTracking();

        // Type + free-text filters translate to SQL (indexed / LIKE).
        if (!string.IsNullOrWhiteSpace(type))
        {
            var wanted = type;
            q = q.Where(p => p.Type == wanted);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            // Latin matches the raw columns; Arabic matches the normalised ones.
            // Comparing raw Arabic would mean a user searching أشعة never finds a
            // facility stored as اشعة — the same word, spelled the other way.
            var raw = $"%{text}%";
            var norm = $"%{ArabicText.Normalize(text)}%";
            q = q.Where(p =>
                (p.NameEn != null && EF.Functions.Like(p.NameEn, raw)) ||
                (p.AddressEn != null && EF.Functions.Like(p.AddressEn, raw)) ||
                (p.NameArNorm != null && EF.Functions.Like(p.NameArNorm, norm)) ||
                (p.AddressArNorm != null && EF.Functions.Like(p.AddressArNorm, norm)));
        }

        // Haversine cannot be translated by EF/SQLite, so materialize then compute
        // distance in memory, apply the radius cutoff, and sort ascending.
        var rows = await q.ToListAsync(ct).ConfigureAwait(false);

        return rows
            .Select(p => new Hit(p, Distance(lat, lng, p.Lat, p.Lng)))
            .Where(h => radiusKm is null || h.DistanceKm <= radiusKm.Value)
            .OrderBy(h => h.DistanceKm)
            // Nearest-N. The sort runs first so the cap keeps the closest results
            // rather than an arbitrary slice.
            .Take(limit)
            .ToList();
    }

    /// <summary>Great-circle distance in kilometres between two points.</summary>
    internal static double DistanceKm(double lat1, double lng1, double lat2, double lng2) =>
        Distance(lat1, lng1, lat2, lng2);

    // Great-circle distance in kilometers (R = 6371 km).
    private static double Distance(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) +
                (Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                 Math.Sin(dLng / 2) * Math.Sin(dLng / 2));
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
