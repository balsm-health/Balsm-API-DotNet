using Balsm.CareDirectory.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Balsm.CareDirectory.Api.Controllers;

[ApiController]
[Route("care")]
public sealed class CareController(IMediator mediator) : ControllerBase
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
    // Identical for every caller and changes only when a pack set is published,
    // so it caches under the same policy as the rest of the directory.
    [HttpGet("packs")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CareDirectoryCachePolicy.Name)]
    public async Task<IActionResult> Packs(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMapPacksQuery(), ct);
        return Ok(new { data = result });
    }
}
