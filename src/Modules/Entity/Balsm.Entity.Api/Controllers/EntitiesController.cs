using Balsm.Infrastructure.Extensions;
using Balsm.Entity.Application.Commands;
using Balsm.Entity.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Entity.Api.Controllers;

[ApiController]
[Route("api/v1/admin/entities")]
public sealed class EntitiesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid workspaceId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => (await mediator.Send(new ListEntitiesQuery(workspaceId, includeInactive), ct)).ToActionResult();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => (await mediator.Send(new GetEntityByIdQuery(id), ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEntityRequest req, CancellationToken ct)
        => (await mediator.Send(new CreateEntityCommand(req.WorkspaceId, req.Name, req.TypeCode, req.RegistrationNumber), ct)).ToActionResult();

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEntityRequest req, CancellationToken ct)
        => (await mediator.Send(new UpdateEntityCommand(id, req.Name, req.RegistrationNumber), ct)).ToActionResult();

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateEntityCommand(id), ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => (await mediator.Send(new ReactivateEntityCommand(id), ct)).ToActionResult();
}

public sealed record CreateEntityRequest(Guid WorkspaceId, string Name, string TypeCode, string? RegistrationNumber);
public sealed record UpdateEntityRequest(string Name, string? RegistrationNumber);
