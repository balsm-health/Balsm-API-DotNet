using Balsam.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsam.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/status")]
public class AdminStatusController(ServerStatusService statusService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(statusService.GetStatus());
}
