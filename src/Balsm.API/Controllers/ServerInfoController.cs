using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Controllers;

[ApiController]
[Route("api/v1/server-info")]
public class ServerInfoController : ControllerBase
{
    private static readonly DateTime _startTime = DateTime.UtcNow;

    [HttpGet]
    public IActionResult Get()
    {
        var version = typeof(ServerInfoController).Assembly
            .GetName().Version?.ToString() ?? "0.0.0";

        var uptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds;

        var deploymentMode = Environment.GetEnvironmentVariable("DeploymentMode") ?? "Standalone";

        return Ok(new
        {
            version,
            mode = deploymentMode,
            uptime_seconds = uptimeSeconds
        });
    }
}
