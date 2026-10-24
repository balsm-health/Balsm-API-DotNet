using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Disclosure.Api.Controllers;

/// <summary>Liveness probe for the Disclosure module.</summary>
[ApiController]
[Route("disclosure/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /disclosure/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("Disclosure_Health")]
    [EndpointSummary("Disclosure module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "Disclosure",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
