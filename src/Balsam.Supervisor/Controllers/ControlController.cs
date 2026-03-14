using Balsam.Supervisor.Models;
using Balsam.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsam.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/control")]
public class AdminControlController(ServerStatusService statusService) : ControllerBase
{
    [HttpPost("restart")]
    public IActionResult Restart()
    {
        statusService.RequestRestart();
        return Ok(new { Message = "Restart signal sent. The service manager will restart the process." });
    }

    [HttpPost("mode")]
    public async Task<IActionResult> ChangeMode([FromBody] ModeChangeRequest request)
    {
        if (request.Mode is not "local" and not "public")
        {
            return BadRequest(new { Message = "Mode must be 'local' or 'public'" });
        }

        await statusService.SwitchModeAsync(request.Mode, request.Port);
        return Ok(new { Message = $"Switching to {request.Mode} mode. The process will restart." });
    }
}
