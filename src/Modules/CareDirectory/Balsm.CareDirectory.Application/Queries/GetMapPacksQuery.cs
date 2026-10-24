using MediatR;

namespace Balsm.CareDirectory.Application.Queries;

/// <summary>
/// The catalogue of downloadable offline artifacts — one entry per Egyptian
/// governorate, each carrying a basemap and a places snapshot.
///
/// Takes one parameter, <see cref="Lang"/>: which language
/// <see cref="MapPackDto.Name"/> comes back in. Everything else about the
/// catalogue is identical for every caller — deciding which packs are worth
/// downloading is the app's job, from <see cref="MapPackDto.Bounds"/> — so it
/// still caches and serves anonymously, just varied by this one query
/// parameter (see <c>CareDirectoryCachePolicy</c>'s VaryByQuery).
/// </summary>
public sealed record GetMapPacksQuery(
    /// "en" or "ar". Anything else, or omitted, resolves to "en" — the
    /// handler never fails a request over an unrecognised language.
    string? Lang) : IRequest<IReadOnlyList<MapPackDto>>;

/// <summary>
/// One downloadable artifact. Versioned on its own so that refreshing places
/// never re-downloads a basemap.
/// </summary>
public sealed record MapPackArtifactDto(
    /// YYYYMMDD. For a basemap the date of the OSM data it contains; for
    /// places, the night it was exported — which is what the UI shows as
    /// "places as of …".
    string Version,
    long SizeBytes,
    /// Verified after download: a truncated basemap that renders half a city,
    /// or a snapshot missing half a governorate's pharmacies, is worse than a
    /// failed download.
    string Sha256,
    string Url,
    /// Places only. Shown before download so the size means something.
    int? Count);

/// <summary>
/// Wire DTO for GET /care/packs.
///
/// The API never serves artifact bytes — the urls point at object storage.
/// Hundreds of megabytes through the app servers would compete with the
/// request budget of every other endpoint, and resumable range requests are
/// something a CDN already does correctly.
///
/// A governorate appears only once both artifacts exist. Half a pack is not
/// offerable: a basemap with no places is a street map with no pharmacies on
/// it, and places with no basemap are pins floating on nothing.
/// </summary>
public sealed record MapPackDto(
    /// Stable governorate slug ("cairo"), keyed on rather than a name.
    string Id,
    /// In the language <see cref="GetMapPacksQuery.Lang"/> requested. Only one
    /// comes back on the wire — resolving it server-side means the app never
    /// carries a name it is not displaying. The app's own local storage keeps
    /// every language it has fetched, so switching the app's language later
    /// does not require a re-fetch just to show a cached governorate's name.
    string Name,
    /// [west, south, east, north] — describes the governorate rather than
    /// either artifact, so the app can tell whether a pack covers the viewport
    /// without opening anything.
    IReadOnlyList<double> Bounds,
    MapPackArtifactDto Basemap,
    MapPackArtifactDto Places);
