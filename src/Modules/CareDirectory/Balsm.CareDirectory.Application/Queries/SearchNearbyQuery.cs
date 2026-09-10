using MediatR;

namespace Balsm.CareDirectory.Application.Queries;

/// <summary>
/// Free-text + geo search over the public care directory. Optional filters:
/// <paramref name="RadiusKm"/> (km cutoff), <paramref name="Type"/>
/// (hospital|clinic|pharmacy|lab|scan|store), and <paramref name="Query"/>
/// (name/address contains). Results are ordered ascending by distance and
/// capped at <paramref name="Limit"/> nearest.
///
/// The cap is not optional: the Egyptian directory holds ~19k places, so a 10 km
/// query around central Cairo matches ~3,900 and a 50 km one ~9,000. Shipping
/// those whole to a phone is megabytes of JSON per pan.
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
    public const int DefaultLimit = 200;

    /// Hard ceiling, whatever the caller asks for.
    public const int MaxLimit = 500;

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
