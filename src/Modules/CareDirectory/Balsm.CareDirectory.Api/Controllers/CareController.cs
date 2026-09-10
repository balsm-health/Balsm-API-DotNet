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
}
