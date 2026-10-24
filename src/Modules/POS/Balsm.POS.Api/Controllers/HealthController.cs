using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.POS.Api.Controllers;

/// <summary>Liveness probe for the POS module.</summary>
[ApiController]
[Route("pos/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /pos/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("POS_Health")]
    [EndpointSummary("POS module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "POS",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
