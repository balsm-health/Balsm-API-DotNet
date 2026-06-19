using Balsm.Account.Application.Commands;
using Balsm.Account.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Balsm.Account.Api.Controllers;

[ApiController]
[Route("account")]
[Authorize]
public sealed class AccountController(IMediator mediator) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? Guid.Empty.ToString());

    // GET /account/self  (T104a, FR-006/048, SelfOnly)
    [HttpGet("self")]
    public async Task<IActionResult> GetSelf(CancellationToken ct)
    {
        var result = await mediator.Send(new GetSelfQuery(CurrentUserId), ct);
        return Ok(new
        {
            data = new
            {
                user_id = result.UserId,
                handle = result.Handle,
                display_name = result.DisplayName,
                bio = result.Bio,
                country_code = result.CountryCode,
                preferred_language = result.PreferredLanguage,
                deletion_state = result.DeletionState,
                dob_year = result.DobYear
            }
        });
    }

    // POST /account/handle/check  (T106, FR-002/003)
    [HttpPost("handle/check")]
    public async Task<IActionResult> CheckHandle([FromBody] CheckHandleRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CheckHandleQuery(req.Handle), ct);
        return Ok(new { data = new { available = result.Available, reason = result.Reason } });
    }

    // POST /account/handle/claim  (T107, FR-002/008/304, ActiveAccount)
    [HttpPost("handle/claim")]
    public async Task<IActionResult> ClaimHandle([FromBody] ClaimHandleRequest req, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(new ClaimHandleCommand(CurrentUserId, req.Handle), ct);
            return Ok(new { data = new { handle = result.Handle } });
        }
        catch (HandleConflictException ex)
        {
            return Conflict(new { error = new { code = "HandleTaken", suggestions = ex.Suggestions } });
        }
    }

    // POST /account/dob  (T108a, FR-047/301a, ActiveAccount)
    [HttpPost("dob")]
    public async Task<IActionResult> SetDob([FromBody] SetDobRequest req, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new SetDobCommand(CurrentUserId, req.DateOfBirth), ct);
            return Ok(new { data = new { set = true } });
        }
        catch (UnderEighteenException)
        {
            return UnprocessableEntity(new { error = new { code = "UnderEighteen" } });
        }
    }

    // PATCH /account/country  (T168, FR-302/044)
    [HttpPatch("country")]
    public async Task<IActionResult> ChangeCountry([FromBody] ChangeCountryRequest req, CancellationToken ct)
    {
        await mediator.Send(new ChangeCountryCommand(CurrentUserId, req.CountryCode), ct);
        return Ok(new { data = new { country_code = req.CountryCode } });
    }

    // PATCH /account/language  (T169, FR-301)
    [HttpPatch("language")]
    public async Task<IActionResult> ChangeLanguage([FromBody] ChangeLanguageRequest req, CancellationToken ct)
    {
        await mediator.Send(new ChangeLanguageCommand(CurrentUserId, req.Language), ct);
        return Ok(new { data = new { preferred_language = req.Language } });
    }
}

public sealed record CheckHandleRequest(string Handle);
public sealed record ClaimHandleRequest(string Handle);
public sealed record SetDobRequest(DateOnly DateOfBirth);
public sealed record ChangeCountryRequest(string CountryCode);
public sealed record ChangeLanguageRequest(string Language);
