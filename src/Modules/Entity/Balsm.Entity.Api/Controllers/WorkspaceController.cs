using Balsm.Infrastructure.Extensions;
using Balsm.Entity.Application.Commands;
using Balsm.Entity.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Entity.Api.Controllers;

[ApiController]
[Route("api/v1/admin/workspace")]
public sealed class WorkspaceController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => (await mediator.Send(new GetWorkspaceQuery(), ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceRequest req, CancellationToken ct)
        => (await mediator.Send(new CreateWorkspaceCommand(req.Name, req.Slug, req.Locale ?? "en"), ct)).ToActionResult();

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkspaceRequest req, CancellationToken ct)
        => (await mediator.Send(new UpdateWorkspaceCommand(id, req.Name, req.Locale ?? "en"), ct)).ToActionResult();
}

public sealed record CreateWorkspaceRequest(string Name, string Slug, string? Locale);
public sealed record UpdateWorkspaceRequest(string Name, string? Locale);
