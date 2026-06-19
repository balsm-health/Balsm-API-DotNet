using Balsm.Deletion.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Balsm.Deletion.Api.Controllers;

[ApiController]
[Route("deletion")]
[Authorize]
public sealed class DeletionController(IMediator mediator) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? Guid.Empty.ToString());

    [HttpPost("intake")]
    public async Task<IActionResult> Intake([FromBody] IntakeRequest req, CancellationToken ct)
    {
        await mediator.Send(new IntakeDeletionCommand(CurrentUserId, req.CountryCode, req.ReasonCode), ct);
        return Ok(new { data = new { grace_until = DateTime.UtcNow.AddDays(7) } });
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        try
        {
            await mediator.Send(new CancelDeletionCommand(CurrentUserId), ct);
            return Ok(new { data = new { cancelled = true } });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = new { code = "CancellationFailed", message = ex.Message } });
        }
    }
}

public sealed record IntakeRequest(string CountryCode, string? ReasonCode);
