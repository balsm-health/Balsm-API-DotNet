using Balsm.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Supervisor.Controllers;

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

    // Mode switching moved to GET/PUT /api/v1/admin/mode (AdminModeController) per the
    // http-admin-api.yaml contract.
}
