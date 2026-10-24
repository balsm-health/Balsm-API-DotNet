using Balsm.Sessions.Infrastructure.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Balsm.Sessions.Api.Controllers;

[ApiController]
[Route("status")]
[AllowAnonymous]
public sealed class StatusController(IMemoryCache cache) : ControllerBase
{
    // GET /status  (T167b) — no auth, used by load balancers + incident dashboards
    [HttpGet]
    public IActionResult Get()
    {
        var health = cache.Get<HealthStatus>(StatusHealthJob.CacheKey);
        var status = health?.Status ?? "starting";
        var statusCode = status == "operational" ? 200 : 503;

        return StatusCode(statusCode, new
        {
            data = new
            {
                status,
                checked_at = health?.CheckedAt,
                version = typeof(StatusController).Assembly.GetName().Version?.ToString()
            }
        });
    }
}
