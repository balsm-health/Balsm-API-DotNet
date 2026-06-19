using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Inventory.Api.Controllers;

/// <summary>Liveness probe for the Inventory module.</summary>
[ApiController]
[Route("inventory/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /inventory/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("Inventory_Health")]
    [EndpointSummary("Inventory module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "Inventory",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
