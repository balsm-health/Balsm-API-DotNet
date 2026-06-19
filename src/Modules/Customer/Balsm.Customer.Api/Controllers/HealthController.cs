using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Balsm.Customer.Api.Controllers;

/// <summary>Liveness probe for the Customer module.</summary>
[ApiController]
[Route("customer/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    // GET /customer/health — module liveness (no auth, no DB). Readiness is gated globally by ReadinessGate.
    [HttpGet]
    [EndpointName("Customer_Health")]
    [EndpointSummary("Customer module liveness probe")]
    public IActionResult Get() => Ok(new
    {
        data = new
        {
            status = "Healthy",
            module = "Customer",
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            timestamp = DateTime.UtcNow
        }
    });
}
