using Balsm.CareDirectory.Application.Queries;
using Balsm.CareDirectory.Domain;
using Balsm.CareDirectory.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.CareDirectory.Infrastructure.Handlers;

public sealed class SearchNearbyHandler(CareDirectoryDbContext db)
    : IRequestHandler<SearchNearbyQuery, IReadOnlyList<CareEntityDto>>
{
    public async Task<IReadOnlyList<CareEntityDto>> Handle(SearchNearbyQuery query, CancellationToken ct)
    {
        var hits = await CarePlaceSearch.FindAsync(
            db, query.Lat, query.Lng, query.RadiusKm, query.Type, query.Query, query.EffectiveLimit, ct)
            .ConfigureAwait(false);

        return hits
            .Select(h => new CareEntityDto(
                h.Place.Id,
                h.Place.Type,
                h.Place.NameEn,
                h.Place.NameAr,
                h.Place.AddressEn,
                h.Place.AddressAr,
                h.Place.Lat,
                h.Place.Lng,
                h.Place.Hours,
                h.Place.Phone,
                h.DistanceKm,
                h.Place.Rating))
            .ToList();
    }
}

/// <summary>Map pins for the current viewport — see <see cref="SearchPinsQuery"/>.</summary>
public sealed class SearchPinsHandler(CareDirectoryDbContext db)
    : IRequestHandler<SearchPinsQuery, IReadOnlyList<CarePinDto>>
{
    public async Task<IReadOnlyList<CarePinDto>> Handle(SearchPinsQuery query, CancellationToken ct)
    {
        var hits = await CarePlaceSearch.FindAsync(
            db, query.Lat, query.Lng, query.RadiusKm, query.Type, query.Query, query.EffectiveLimit, ct)
            .ConfigureAwait(false);

        return hits
            .Select(h => new CarePinDto(h.Place.Id, h.Place.Type, h.Place.Lat, h.Place.Lng))
            .ToList();
    }
}

/// <summary>One place by id — what a tapped pin needs.</summary>
public sealed class GetCarePlaceHandler(CareDirectoryDbContext db)
    : IRequestHandler<GetCarePlaceQuery, CareEntityDto?>
{
    public async Task<CareEntityDto?> Handle(GetCarePlaceQuery query, CancellationToken ct)
    {
        var place = await db.CarePlaces.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.Id, ct)
            .ConfigureAwait(false);

        if (place is null) return null;

        // Distance is relative to wherever the caller is, so it is computed here
        // rather than stored; callers that omit a position get null.
        double? distance = query.Lat is null || query.Lng is null
            ? null
            : CarePlaceSearch.DistanceKm(query.Lat.Value, query.Lng.Value, place.Lat, place.Lng);

        return new CareEntityDto(
            place.Id, place.Type, place.NameEn, place.NameAr, place.AddressEn, place.AddressAr,
            place.Lat, place.Lng, place.Hours, place.Phone, distance ?? 0, place.Rating);
    }
}
