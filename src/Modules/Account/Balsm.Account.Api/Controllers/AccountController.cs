using Balsm.Account.Application.Commands;
using Balsm.Account.Application.Queries;
using Balsm.Account.Domain;
using Balsm.Infrastructure.Extensions;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

        if (result.IsFailure)
            return result.ToProblem(HttpContext);

        var self = result.Value!;

        return Ok(new
        {
            data = new
            {
                user_id = self.UserId,
                first_name = self.FirstName,
                last_name = self.LastName,
                handle = self.Handle,
                display_name = self.DisplayName,
                bio = self.Bio,
                gender = self.Gender,
                nationality = self.Nationality,
                phone = self.Phone,
                country_code = self.CountryCode,
                preferred_language = self.PreferredLanguage,
                deletion_state = self.DeletionState,
                dob_year = self.DobYear,
                date_of_birth = self.DateOfBirth,
                national_id = self.NationalId
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

    // PATCH /account/profile — display name, bio, gender, nationality, phone,
    // date of birth (encrypted, 18+ gated), national ID (encrypted). Partial:
    // null leaves a field unchanged, "" clears it.
    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        try
        {
            await mediator.Send(new UpdateProfileCommand(
                CurrentUserId,
                req.FirstName,
                req.LastName,
                req.Bio,
                req.Gender,
                req.Nationality,
                req.Phone,
                req.DateOfBirth,
                req.NationalId), ct);
            return Ok(new { data = new { updated = true } });
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
public sealed record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    string? Bio,
    string? Gender,
    string? Nationality,
    string? Phone,
    DateOnly? DateOfBirth,
    string? NationalId);
public sealed record ChangeCountryRequest(string CountryCode);
public sealed record ChangeLanguageRequest(string Language);
