using Balsm.CareDirectory.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Balsm.CareDirectory.Api.Controllers;

[ApiController]
[Route("care")]
public sealed class CareController(IMediator mediator, IHostEnvironment environment) : ControllerBase
{
    // GET /care/entities — public "nearby health places" directory (NON-PHI).
    // radius_km binds explicitly by its snake_case wire name (query-string binding
    // does not apply the JSON naming policy).
    [HttpGet("entities")]
    [AllowAnonymous]
    // Public NON-PHI reference data that changes only on import, and the map
    // re-queries on every pan — so the same coordinates are asked for
    // repeatedly. Safe to cache and serve to any caller; nothing here is
    // patient-scoped.
    [OutputCache(PolicyName = CareDirectoryCachePolicy.Name)]
    public async Task<IActionResult> Entities(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery(Name = "radius_km")] double? radiusKm,
        [FromQuery] string? type,
        [FromQuery] string? q,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SearchNearbyQuery(lat, lng, radiusKm, type, q, limit), ct);
        return Ok(new { data = result });
    }

    // GET /care/pins — the same search projected to map pins (NON-PHI).
    //
    // Separate from /care/entities rather than a flag on it: the shapes, the
    // limits and the callers all differ, and a pin response must stay cheap
    // enough that adding a field to it is a deliberate decision.
    [HttpGet("pins")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CareDirectoryCachePolicy.Name)]
    public async Task<IActionResult> Pins(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery(Name = "radius_km")] double? radiusKm,
        [FromQuery] string? type,
        [FromQuery] string? q,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SearchPinsQuery(lat, lng, radiusKm, type, q, limit), ct);
        return Ok(new { data = result });
    }

    // GET /care/entities/{id} — one place, for a tapped pin.
    [HttpGet("entities/{id:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CareDirectoryCachePolicy.Name)]
    public async Task<IActionResult> Entity(
        Guid id,
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetCarePlaceQuery(id, lat, lng), ct);
        return result is null ? NotFound() : Ok(new { data = result });
    }

    // GET /care/packs — catalogue of downloadable offline map packs (NON-PHI).
    //
    // The catalogue only; pack BYTES are served from object storage, whose URL
    // each entry carries. Routing hundreds of megabytes through the app servers
    // would compete with the request budget of every other endpoint, and
    // resumable range requests are something a CDN already does correctly.
    //
    // Changes only when a pack set is published and varies only by `lang`, so
    // it caches under the same policy as the rest of the directory (the
    // policy's VaryByQuery includes `lang` for exactly this endpoint).
    [HttpGet("packs")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CareDirectoryCachePolicy.Name)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EndpointSummary("List downloadable offline map packs")]
    [EndpointDescription("One entry per Egyptian governorate with both a basemap and a places snapshot published. " +
        "`name` comes back in the requested `lang` (\"en\" or \"ar\"; anything else defaults to \"en\").")]
    [EndpointName("CareDirectory_Packs")]
    [Tags("CareDirectory/Packs")]
    public async Task<IActionResult> Packs([FromQuery] string? lang, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMapPacksQuery(lang), ct);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var resolved = result.Select(p => ResolvePackUrls(p, baseUrl)).ToList();
        return Ok(new { data = resolved });
    }

    // GET /care/packs/places/{file} — serves exported offline places snapshots locally
    [HttpGet("packs/places/{file}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EndpointSummary("Download an offline places snapshot")]
    [EndpointName("CareDirectory_DownloadPlaces")]
    [Tags("CareDirectory/Packs")]
    public IActionResult DownloadPlaces(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || file.Contains("..") || file.Contains('/') || file.Contains('\\'))
        {
            return BadRequest();
        }

        string? foundPath = null;
        foreach (var root in new[] { AppContext.BaseDirectory, environment.ContentRootPath, Directory.GetCurrentDirectory() })
        {
            var candidate = Path.Combine(root, "data", "map-packs", "places", file);
            if (System.IO.File.Exists(candidate))
            {
                foundPath = candidate;
                break;
            }
        }

        if (foundPath is null)
        {
            return NotFound();
        }

        return PhysicalFile(foundPath, "application/x-ndjson", file, enableRangeProcessing: true);
    }

    private static MapPackDto ResolvePackUrls(MapPackDto pack, string baseUrl)
    {
        var basemap = ResolveArtifactUrl(pack.Basemap, baseUrl);
        var places = ResolveArtifactUrl(pack.Places, baseUrl);
        return (basemap == pack.Basemap && places == pack.Places)
            ? pack
            : new MapPackDto(pack.Id, pack.Name, pack.Bounds, basemap, places);
    }

    private static MapPackArtifactDto ResolveArtifactUrl(MapPackArtifactDto artifact, string baseUrl)
    {
        if (artifact.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            artifact.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return artifact;
        }

        var absoluteUrl = artifact.Url.StartsWith('/')
            ? $"{baseUrl}{artifact.Url}"
            : $"{baseUrl}/{artifact.Url}";

        return new MapPackArtifactDto(artifact.Version, artifact.SizeBytes, artifact.Sha256, absoluteUrl, artifact.Count);
    }
}
