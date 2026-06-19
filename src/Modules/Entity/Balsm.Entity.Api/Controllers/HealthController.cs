using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Entity.Api.Controllers;

/// <summary>Liveness probe for the Entity module.</summary>
[ApiController]
[Route("entity/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /entity/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("Entity_Health")]
    [EndpointSummary("Entity module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "Entity",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
