using Balsm.Identity.Application.Commands;
using Balsm.Identity.Application.Queries;
using Balsm.Infrastructure.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
public class UsersController(IMediator mediator) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var email = User.Identity?.Name ?? string.Empty;
        return (await mediator.Send(new GetCurrentAdminUserQuery(email), ct)).ToActionResult();
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest req, CancellationToken ct)
    {
        var email = User.Identity?.Name ?? string.Empty;
        return (await mediator.Send(new UpdateCurrentAdminUserCommand(email, req.DisplayName, req.Locale ?? "en"), ct)).ToActionResult();
    }

    [HttpGet("ping")]
    public IActionResult Ping() =>
        Ok(new { Module = "Identity", Status = "OK", Timestamp = DateTime.UtcNow });
}

public sealed record UpdateMeRequest(string DisplayName, string? Locale);
