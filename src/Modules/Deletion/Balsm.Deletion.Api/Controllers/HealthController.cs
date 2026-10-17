using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Deletion.Api.Controllers;

/// <summary>Liveness probe for the Deletion module.</summary>
[ApiController]
[Route("deletion/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /deletion/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("Deletion_Health")]
    [EndpointSummary("Deletion module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "Deletion",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
