using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/mode")]
public class AdminModeController(ServerStatusService statusService) : ControllerBase
{
    /// <summary>GET /api/v1/admin/mode — current deployment mode + bound ports.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        var status = statusService.GetStatus();
        return Ok(new
        {
            mode = status.Mode,
            http_port = status.HttpPort,
            https_port = status.HttpsPort
        });
    }

    /// <summary>PUT /api/v1/admin/mode — switch deployment mode; the process restarts.</summary>
    [HttpPut]
    public async Task<IActionResult> Put([FromBody] ModeChangeRequest request)
    {
        if (request.Mode is not "local" and not "network" and not "public")
            return BadRequest(new { message = "Mode must be 'local', 'network', or 'public'" });

        await statusService.SwitchModeAsync(request.Mode, request.Port);
        return Ok(new { message = $"Switching to {request.Mode} mode. The process will restart." });
    }
}
