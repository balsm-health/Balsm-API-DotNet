using System.Text.RegularExpressions;
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
public sealed partial class CareController(IMediator mediator, IHostEnvironment environment) : ControllerBase
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
    //
    // Places URLs are stored as root-relative paths ("/care/packs/places/...")
    // and returned as-is. The client resolves them against its configured
    // baseUrl, which already knows the correct scheme+host for the device.
    // This avoids OutputCache host-poisoning: a response cached from
    // localhost would otherwise serve broken URLs to a phone on the LAN.
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
        return Ok(new { data = result });
    }

    // GET /care/packs/places/{file} — serves exported offline places snapshots locally.
    //
    // Filename is validated against a strict pattern to prevent path traversal.
    // The canonical path is checked to ensure it stays inside the target directory.
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
        if (!ValidPlacesFilename().IsMatch(file))
        {
            return BadRequest();
        }

        string? foundPath = null;
        foreach (var root in new[] { AppContext.BaseDirectory, environment.ContentRootPath })
        {
            var targetDir = Path.GetFullPath(Path.Combine(root, "data", "map-packs", "places"));
            var candidate = Path.GetFullPath(Path.Combine(targetDir, file));

            // Belt-and-suspenders: even after the regex, verify the resolved
            // path is actually inside the expected directory.
            if (!candidate.StartsWith(targetDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !candidate.Equals(targetDir, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

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

    /// <summary>Strict allowlist: lowercase slug + YYYYMMDD date + expected extension.</summary>
    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?-\d{8}\.ndjson\.gz$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPlacesFilename();
}
