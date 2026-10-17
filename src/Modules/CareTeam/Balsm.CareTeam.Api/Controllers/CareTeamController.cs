using Balsm.CareTeam.Application.Commands;
using Balsm.CareTeam.Application.Queries;
using Balsm.CareTeam.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Balsm.CareTeam.Api.Controllers;

/// <summary>
/// The patient's own care team, mirrored to the Balsm cloud. Every operation is
/// scoped to the caller's user id taken from the token — never from the body.
/// </summary>
[ApiController]
[Route("care-team")]
[Authorize]
[Produces("application/json")]
[Tags("CareTeam/Providers")]
public sealed class CareTeamController(IMediator mediator) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? Guid.Empty.ToString());

    /// <summary>Rows changed since the cursor, tombstones included.</summary>
    /// <remarks>Requires an authenticated patient; returns only the caller's own rows.</remarks>
    // GET /care-team/providers?health_profile_id=…&since=…  (FR-508/FR-510)
    [HttpGet("providers")]
    [EndpointName("CareTeam_PullProviders")]
    [EndpointSummary("Pull care-team rows changed since a cursor")]
    [EndpointDescription("Incremental sync feed. Tombstoned rows are returned with is_deleted=true so a delete on one device propagates to the others.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Pull(
        [FromQuery(Name = "health_profile_id")] Guid healthProfileId,
        [FromQuery(Name = "since")] DateTime? since,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new PullCareProvidersQuery(
                CurrentUserId, healthProfileId, since,
                Actor: User.FindFirstValue(ClaimTypes.NameIdentifier),
                SourceIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
                CorrelationId: HttpContext.TraceIdentifier), ct);

        return Ok(new
        {
            data = result.Value!.Select(p => new
            {
                id = p.Id,
                health_profile_id = p.HealthProfileId,
                type = p.Type,
                name = p.Name,
                specialty = p.Specialty,
                phone = p.Phone,
                phone2 = p.Phone2,
                email = p.Email,
                clinic = p.Clinic,
                address = p.Address,
                map_url = p.MapUrl,
                notes = p.Notes,
                created_at = p.CreatedAt,
                updated_at = p.UpdatedAt,
                is_deleted = p.IsDeleted
            })
        });
    }

    /// <summary>Creates or overwrites one provider, idempotent on the client-minted id.</summary>
    /// <remarks>Requires an authenticated patient. A row owned by another user answers 404, not 403.</remarks>
    // POST /care-team/providers  (FR-505/FR-506)
    [HttpPost("providers")]
    // Controller-level [Consumes] would be an action constraint on EVERY action:
    // GET and DELETE send no Content-Type, so nothing would match and the SPA
    // fallback would answer 200 text/html in place of the endpoint.
    [Consumes("application/json")]
    [EndpointName("CareTeam_UpsertProvider")]
    [EndpointSummary("Create or overwrite one care-team row")]
    [EndpointDescription("Idempotent on id so an at-least-once outbox drain cannot duplicate. A tombstoned id is refused with 409 rather than resurrected.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upsert([FromBody] UpsertCareProviderRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpsertCareProviderCommand(
            req.Id, CurrentUserId, req.HealthProfileId, req.Type, req.Name,
            req.Specialty, req.Phone, req.Phone2, req.Email, req.Clinic,
            req.Address, req.MapUrl, req.Notes, req.CreatedAt), ct);

        if (result.IsSuccess) return Ok(new { data = new { id = req.Id } });
        if (result.Error == CareTeamErrors.NotFound) return NotFound(new { error = result.Error.Code });
        if (result.Error == CareTeamErrors.Tombstoned) return Conflict(new { error = result.Error!.Code });
        return UnprocessableEntity(new { error = result.Error!.Code });
    }

    /// <summary>Tombstones one provider so the removal propagates to other devices.</summary>
    /// <remarks>Requires an authenticated patient. Idempotent; unknown and not-owned both answer 404.</remarks>
    // DELETE /care-team/providers/{id}  (FR-507)
    [HttpDelete("providers/{id:guid}")]
    [EndpointName("CareTeam_DeleteProvider")]
    [EndpointSummary("Tombstone one care-team row")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteCareProviderCommand(id, CurrentUserId), ct);
        if (result.IsSuccess) return NoContent();
        return NotFound(new { error = result.Error!.Code });
    }
}

/// <summary>Request body for creating or overwriting one care-team row.</summary>
/// <param name="Id">Device-minted UUIDv7. Authoritative on both sides — the server never assigns one.</param>
/// <param name="CreatedAt">Device clock, used for first-write ordering only; updated_at is the server's.</param>
public sealed record UpsertCareProviderRequest(
    Guid Id,
    Guid HealthProfileId,
    string Type,
    string Name,
    string? Specialty,
    string? Phone,
    string? Phone2,
    string? Email,
    string? Clinic,
    string? Address,
    string? MapUrl,
    string? Notes,
    DateTime CreatedAt);
