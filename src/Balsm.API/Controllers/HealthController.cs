using Balsm.Infrastructure.Lifecycle;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController(ReadinessGate gate) : ControllerBase
{
    private static readonly DateTime _startTime = DateTime.UtcNow;

    [HttpGet]
    public IActionResult Get()
    {
        var uptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds;
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            ready = gate.IsReady,
            version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            uptime_seconds = uptimeSeconds,
            not_ready_reason = gate.IsReady ? null : gate.Reason,
            deploy_check = "cd-canary-2026-07-06"
        });
    }
}
