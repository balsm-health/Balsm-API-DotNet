using Balsm.Disclosure.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Balsm.Disclosure.Api.Controllers;

[ApiController]
[Route("disclosure")]
public sealed class DisclosureController(IMediator mediator) : ControllerBase
{
    // POST /disclosure/accept  (T091a, FR-040)
    [HttpPost("accept")]
    [Authorize]
    public async Task<IActionResult> Accept([FromBody] AcceptDisclosureRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub") ?? Guid.Empty.ToString());

        var result = await mediator.Send(
            new AcceptDisclosureCommand(userId, req.DisclosureId, req.Version,
                req.CountryCode, req.SupervisoryAuthority, req.Language), ct);

        return Ok(new { data = new { acceptance_id = result.AcceptanceId, already_accepted = result.WasAlreadyAccepted } });
    }
}

public sealed record AcceptDisclosureRequest(
    string DisclosureId, string Version,
    string CountryCode, string SupervisoryAuthority, string Language);
