using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.CareDirectory.Api.Controllers;

/// <summary>Liveness probe for the CareDirectory module.</summary>
[ApiController]
[Route("care/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /care/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("CareDirectory_Health")]
    [EndpointSummary("CareDirectory module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "CareDirectory",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
