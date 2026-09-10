using Balsm.CareDirectory.Application.Queries;
using Balsm.CareDirectory.Domain;
using Balsm.CareDirectory.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareDirectory.Infrastructure.Handlers;

public sealed class SearchNearbyHandler(CareDirectoryDbContext db)
    : IRequestHandler<SearchNearbyQuery, IReadOnlyList<CareEntityDto>>
{
    private const double EarthRadiusKm = 6371.0;

    public async Task<IReadOnlyList<CareEntityDto>> Handle(SearchNearbyQuery query, CancellationToken ct)
    {
        var q = db.CarePlaces.AsNoTracking();

        // Type + free-text filters translate to SQL (indexed / LIKE).
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type;
            q = q.Where(p => p.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            // Latin matches the raw columns; Arabic matches the normalised ones.
            // Comparing raw Arabic would mean a user searching أشعة never finds a
            // facility stored as اشعة — the same word, spelled the other way.
            var raw = $"%{query.Query}%";
            var norm = $"%{ArabicText.Normalize(query.Query)}%";
            q = q.Where(p =>
                (p.NameEn != null && EF.Functions.Like(p.NameEn, raw)) ||
                (p.AddressEn != null && EF.Functions.Like(p.AddressEn, raw)) ||
                (p.NameArNorm != null && EF.Functions.Like(p.NameArNorm, norm)) ||
                (p.AddressArNorm != null && EF.Functions.Like(p.AddressArNorm, norm)));
        }

        // Haversine cannot be translated by EF/SQLite, so materialize then compute
        // distance in memory, apply the radius cutoff, and sort ascending.
        var rows = await q.ToListAsync(ct);

        return rows
            .Select(p => new
            {
                Place = p,
                DistanceKm = Distance(query.Lat, query.Lng, p.Lat, p.Lng)
            })
            .Where(x => query.RadiusKm is null || x.DistanceKm <= query.RadiusKm.Value)
            .OrderBy(x => x.DistanceKm)
            // Nearest-N. The sort runs first so the cap keeps the closest
            // results rather than an arbitrary slice.
            .Take(query.EffectiveLimit)
            .Select(x => new CareEntityDto(
                x.Place.Id,
                x.Place.Type,
                x.Place.NameEn,
                x.Place.NameAr,
                x.Place.AddressEn,
                x.Place.AddressAr,
                x.Place.Lat,
                x.Place.Lng,
                x.Place.Hours,
                x.Place.Phone,
                x.DistanceKm,
                x.Place.Rating))
            .ToList();
    }

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
