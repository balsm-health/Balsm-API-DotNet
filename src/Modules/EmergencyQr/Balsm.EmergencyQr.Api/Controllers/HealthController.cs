using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.EmergencyQr.Api.Controllers;

/// <summary>Liveness probe for the EmergencyQr module.</summary>
[ApiController]
[Route("emergency-qr/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /emergency-qr/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("EmergencyQr_Health")]
    [EndpointSummary("EmergencyQr module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "EmergencyQr",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
