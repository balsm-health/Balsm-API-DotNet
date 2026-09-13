using MediatR;

namespace Balsm.CareDirectory.Application.Queries;

/// <summary>
/// The catalogue of downloadable offline map packs — one per Egyptian
/// governorate.
///
/// Takes no parameters: the catalogue is the same for every caller, which is
/// what lets it be cached and served anonymously. Filtering to "packs near me"
/// is the app's job, from <see cref="MapPackDto.Bounds"/>.
/// </summary>
public sealed record GetMapPacksQuery : IRequest<IReadOnlyList<MapPackDto>>;

/// <summary>
/// Wire DTO for GET /care/packs.
///
/// The API never serves pack bytes — <see cref="Url"/> points at object
/// storage. Hundreds of megabytes through the app servers would compete with
/// the request budget of every other endpoint, and resumable range requests are
/// something a CDN already does correctly.
/// </summary>
public sealed record MapPackDto(
    /// Stable, url-safe governorate id ("cairo"). The app keys on this rather
    /// than a name, which is translated and may be re-spelled upstream.
    string Id,
    string NameEn,
    string NameAr,
    /// Date of the OSM data inside the pack, YYYYMMDD. Compared against an
    /// installed pack to offer an update, and shown wherever its places are.
    string Version,
    long SizeBytes,
    /// Verified after download: a truncated pack that renders half a city is
    /// worse than a failed download.
    string Sha256,
    /// [west, south, east, north] — lets the app decide whether a pack covers
    /// the viewport without opening it.
    IReadOnlyList<double> Bounds,
    string Url);
