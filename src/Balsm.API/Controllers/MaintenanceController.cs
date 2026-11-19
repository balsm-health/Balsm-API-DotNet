using Balsm.Infrastructure.Maintenance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Controllers;

/// <summary>
/// Public, read-only maintenance flag polled by the patient app (on launch, on
/// resume, every few minutes, and on "Check again").
/// </summary>
[ApiController]
[Route("api/v1/maintenance")]
[Tags("Platform/Maintenance")]
public sealed class MaintenanceController(IMaintenanceModeService maintenance) : ControllerBase
{
    /// <summary>Current maintenance mode, admin message and announced end time.</summary>
    /// <remarks>
    /// Anonymous: the app must read it before sign-in, and while sign-in itself is
    /// paused. Carries no PHI and no per-user data, so it is publicly cacheable for
    /// a few seconds. <c>mode</c> is one of <c>off</c>, <c>readonly</c>, <c>block</c>;
    /// <c>message</c> null means "use the default copy"; <c>ends_at</c> null means
    /// "not announced".
    /// </remarks>
    // reason: anonymous by design — read before sign-in, no PHI, no per-user data.
    [AllowAnonymous]
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType<MaintenanceStatusResponse>(StatusCodes.Status200OK)]
    [EndpointName("Platform_GetMaintenance")]
    [EndpointSummary("Get the maintenance mode")]
    [EndpointDescription("Returns { data: { mode, message, ends_at, updated_at } }. Anonymous; safe to poll.")]
    public async Task<ActionResult<MaintenanceStatusResponse>> Get(CancellationToken ct)
    {
        var state = await maintenance.GetAsync(ct);
        Response.Headers.CacheControl = "public, max-age=15";
        return Ok(new MaintenanceStatusResponse(state));
    }
}
