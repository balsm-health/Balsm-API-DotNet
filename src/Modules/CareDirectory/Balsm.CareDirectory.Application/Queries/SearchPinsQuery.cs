using MediatR;

namespace Balsm.CareDirectory.Application.Queries;

/// <summary>
/// Map-pin search: the same narrowing as <see cref="SearchNearbyQuery"/>, but
/// projected to what a pin actually needs.
///
/// A pin is a dot at a coordinate coloured by type — it needs no name, address,
/// phone, hours or rating. Measured against the live directory those fields are
/// most of the payload: a full row is ~340 bytes and a pin ~99, so the same
/// bandwidth carries roughly 3.4x as many. That is the difference between a map
/// that shows a dense knot around the centre and one that covers the viewport.
/// </summary>
public sealed record SearchPinsQuery(
    double Lat,
    double Lng,
    double? RadiusKm,
    string? Type,
    string? Query,
    int? Limit = null) : IRequest<IReadOnlyList<CarePinDto>>
{
    /// Applied when the caller sends no limit.
    public const int DefaultLimit = 1500;

    /// Hard ceiling. At ~99 bytes a pin this is roughly 300 KB.
    public const int MaxLimit = 3000;

    public int EffectiveLimit => Math.Clamp(Limit ?? DefaultLimit, 1, MaxLimit);
}

/// <summary>
/// Wire DTO for GET /care/pins. Deliberately minimal — adding a field here
/// costs bandwidth on every pin in the viewport, and the detail endpoint exists
/// for everything else.
/// </summary>
public sealed record CarePinDto(Guid Id, string Type, double Lat, double Lng);
