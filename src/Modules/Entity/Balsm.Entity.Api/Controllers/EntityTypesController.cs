using Balsm.Infrastructure.Extensions;
using Balsm.Entity.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Entity.Api.Controllers;

[ApiController]
[Route("api/v1/admin/entity-types")]
public sealed class EntityTypesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => (await mediator.Send(new ListEntityTypesQuery(), ct)).ToActionResult();
}
