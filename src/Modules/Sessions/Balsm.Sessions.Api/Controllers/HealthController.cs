using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Sessions.Api.Controllers;

/// <summary>Liveness probe for the Sessions module.</summary>
[ApiController]
[Route("sessions/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /sessions/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("Sessions_Health")]
    [EndpointSummary("Sessions module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "Sessions",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
