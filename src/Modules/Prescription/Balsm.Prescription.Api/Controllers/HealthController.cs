using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Prescription.Api.Controllers;

/// <summary>Liveness probe for the Prescription module.</summary>
[ApiController]
[Route("prescription/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /prescription/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("Prescription_Health")]
    [EndpointSummary("Prescription module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "Prescription",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
