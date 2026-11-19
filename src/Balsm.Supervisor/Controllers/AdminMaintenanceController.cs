using Balsm.Infrastructure.Maintenance;
using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Supervisor.Controllers;

/// <summary>Body for <c>PUT /api/v1/admin/maintenance</c>.</summary>
/// <param name="Mode">One of <c>off</c>, <c>readonly</c>, <c>block</c>.</param>
/// <param name="Message">Optional message to patients (max 280 chars, no PHI); null or empty uses the app's default copy.</param>
/// <param name="EndsAt">Optional announced end time (ISO-8601, future); null means "not announced".</param>
public sealed record UpdateMaintenanceModeRequest(string? Mode, string? Message, DateTimeOffset? EndsAt);

/// <summary>
/// Super-admin control for the platform maintenance mode. Lives in the Supervisor
/// (admin portal) so it is covered by <see cref="AdminAuthMiddleware"/> (admin
/// session cookie, or the loopback CLI token) and does not exist at all where the
/// admin portal is disabled. On cloud runs the mode is set by configuration
/// (<c>Maintenance__Mode</c>) instead.
/// </summary>
[ApiController]
[Route("api/v1/admin/maintenance")]
[Tags("Supervisor/Maintenance")]
public sealed class AdminMaintenanceController(
    IMaintenanceModeService maintenance,
    AdminSessionService sessions) : ControllerBase
{
    /// <summary>Current maintenance state (admin view).</summary>
    /// <remarks>Requires an admin-portal session (super admin).</remarks>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType<MaintenanceStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [EndpointName("Supervisor_GetMaintenance")]
    [EndpointSummary("Get the maintenance mode (admin)")]
    [EndpointDescription("Same payload as the public GET /api/v1/maintenance, behind admin auth.")]
    public async Task<ActionResult<MaintenanceStatusResponse>> Get(CancellationToken ct)
        => Ok(new MaintenanceStatusResponse(await maintenance.GetAsync(ct)));

    /// <summary>Set the maintenance mode, message and announced end time. Audited.</summary>
    /// <remarks>
    /// Requires an admin-portal session (super admin). Writes a
    /// <c>Maintenance/MaintenanceModeChanged</c> audit row naming the admin.
    /// Switching to <c>off</c> clears the message and end time. Takes effect for
    /// clients within the read cache window (~10 s). Idempotent: repeating the same
    /// body leaves the same state.
    /// </remarks>
    [HttpPut]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<MaintenanceStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [EndpointName("Supervisor_UpdateMaintenance")]
    [EndpointSummary("Set the maintenance mode")]
    [EndpointDescription("Sets mode (off | readonly | block), optional message and optional ends_at. Super admin only; audited.")]
    public async Task<ActionResult<MaintenanceStatusResponse>> Put(
        [FromBody] UpdateMaintenanceModeRequest request, CancellationToken ct)
    {
        var result = await maintenance.SetAsync(
            new MaintenanceChange(request.Mode, request.Message, request.EndsAt), ResolveActor(), ct);

        if (result.IsSuccess)
            return Ok(new MaintenanceStatusResponse(result.Value!));

        var error = result.Error!;
        var status = error == MaintenanceErrors.ConfigOverride
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest;
        var problem = new ProblemDetails { Status = status, Title = error.Message, Detail = error.Message };
        problem.Extensions["code"] = error.Code;
        return StatusCode(status, problem);
    }

    private string ResolveActor()
    {
        if (HttpContext.Items.ContainsKey(LocalOsTrustMiddleware.LocalClaimKey))
            return "local-cli";
        return Request.Cookies.TryGetValue(AdminAuthMiddleware.SessionCookieName, out var token)
               && sessions.GetUsername(token) is { Length: > 0 } user
            ? $"admin:{user}"
            : "admin";
    }
}
