using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Identity.Api.Controllers;

/// <summary>Liveness probe for the Identity module.</summary>
[ApiController]
[Route("identity/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /identity/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("Identity_Health")]
    [EndpointSummary("Identity module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "Identity",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
