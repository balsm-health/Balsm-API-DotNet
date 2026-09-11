using MediatR;

namespace Balsm.CareDirectory.Application.Queries;

/// <summary>
/// Free-text + geo search over the public care directory. Optional filters:
/// <paramref name="RadiusKm"/> (km cutoff), <paramref name="Type"/>
/// (hospital|clinic|pharmacy|lab|scan|store), and <paramref name="Query"/>
/// (name/address contains). Results are ordered ascending by distance and
/// capped at <paramref name="Limit"/> nearest.
///
/// The cap is not optional — a 50 km query around Cairo matches thousands and
/// shipping those whole is megabytes of JSON per pan — but it must not be so
/// tight that it becomes the thing users notice. At 200 the cap bound at 1.4 km
/// in central Cairo, so a 25 km search showed a dense knot surrounded by empty
/// map; cities with genuine coverage looked deserted. 500 costs ~166 KB and
/// reaches ~2.1 km there, while every smaller city returns everything it has.
/// </summary>
public sealed record SearchNearbyQuery(
    double Lat,
    double Lng,
    double? RadiusKm,
    string? Type,
    string? Query,
    int? Limit = null) : IRequest<IReadOnlyList<CareEntityDto>>
{
    /// Applied when the caller sends no limit.
    public const int DefaultLimit = 500;

    /// Hard ceiling, whatever the caller asks for.
    public const int MaxLimit = 1000;

    public int EffectiveLimit => Math.Clamp(Limit ?? DefaultLimit, 1, MaxLimit);
}

/// <summary>
/// Wire DTO for GET /care/entities. PascalCase property names map to the
/// snake_case wire keys via the host's global SnakeCaseLower JSON policy
/// (e.g. NameEn -> name_en, DistanceKm -> distance_km, Lat -> lat, Lng -> lng).
/// The Guid Id serializes as a JSON string.
/// </summary>
public sealed record CareEntityDto(
    Guid Id,
    string Type,
    string? NameEn,
    string? NameAr,
    string? AddressEn,
    string? AddressAr,
    double Lat,
    double Lng,
    string? Hours,
    string? Phone,
    double DistanceKm,
    double? Rating);
