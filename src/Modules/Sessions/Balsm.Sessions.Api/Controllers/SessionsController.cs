using Balsm.Sessions.Application.Commands;
using Balsm.Sessions.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Balsm.Sessions.Api.Controllers;

[ApiController]
[Route("sessions")]
[Authorize]
public sealed class SessionsController(IMediator mediator) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? Guid.Empty.ToString());

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await mediator.Send(new ListSessionsQuery(CurrentUserId), ct);
        return Ok(new { data = result.Sessions });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await mediator.Send(new RevokeSessionCommand(id, CurrentUserId), ct);
        return Ok(new { data = new { revoked = true } });
    }

    [HttpDelete]
    public async Task<IActionResult> RevokeAll(CancellationToken ct)
    {
        await mediator.Send(new RevokeAllSessionsCommand(CurrentUserId), ct);
        return Ok(new { data = new { revoked = true } });
    }
}
