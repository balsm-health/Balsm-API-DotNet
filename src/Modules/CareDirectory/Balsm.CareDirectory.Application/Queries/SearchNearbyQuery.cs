using MediatR;

namespace Balsm.CareDirectory.Application.Queries;

/// <summary>
/// Free-text + geo search over the public care directory. Optional filters:
/// <paramref name="RadiusKm"/> (km cutoff), <paramref name="Type"/>
/// (hospital|clinic|pharmacy|lab|scan|store), and <paramref name="Query"/>
/// (name/address contains). Results are ordered ascending by distance.
/// </summary>
public sealed record SearchNearbyQuery(
    double Lat,
    double Lng,
    double? RadiusKm,
    string? Type,
    string? Query) : IRequest<IReadOnlyList<CareEntityDto>>;

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
