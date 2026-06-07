using Balsm.Infrastructure.Extensions;
using Balsm.Entity.Application.Commands;
using Balsm.Entity.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Entity.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
public sealed class BranchesController(IMediator mediator) : ControllerBase
{
    [HttpGet("entities/{entityId:guid}/branches")]
    public async Task<IActionResult> List(Guid entityId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => (await mediator.Send(new ListBranchesQuery(entityId, includeInactive), ct)).ToActionResult();

    [HttpPost("branches")]
    public async Task<IActionResult> Create([FromBody] CreateBranchRequest req, CancellationToken ct)
        => (await mediator.Send(new CreateBranchCommand(req.EntityRootId, req.Name, req.AddressLine, req.City, req.Governorate, req.Phone), ct)).ToActionResult();

    [HttpPatch("branches/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBranchRequest req, CancellationToken ct)
        => (await mediator.Send(new UpdateBranchCommand(id, req.Name, req.AddressLine, req.City, req.Governorate, req.Phone), ct)).ToActionResult();

    [HttpPost("branches/{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateBranchCommand(id), ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    // Hard delete is forbidden (FR-015). Soft-delete via the deactivate endpoint instead.
    [HttpDelete("branches/{id:guid}")]
    public IActionResult RejectHardDelete(Guid id)
        => StatusCode(405,
            new { error = "Hard delete forbidden; use POST branches/{id}/deactivate instead." });

    [HttpPost("branches/{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => (await mediator.Send(new ReactivateBranchCommand(id), ct)).ToActionResult();
}

public sealed record CreateBranchRequest(Guid EntityRootId, string Name, string? AddressLine, string? City, string? Governorate, string? Phone);
public sealed record UpdateBranchRequest(string Name, string? AddressLine, string? City, string? Governorate, string? Phone);
