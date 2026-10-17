using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Auth.Api.Controllers;

/// <summary>Liveness probe for the Auth module.</summary>
[ApiController]
[Route("auth/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /auth/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("Auth_Health")]
    [EndpointSummary("Auth module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "Auth",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
