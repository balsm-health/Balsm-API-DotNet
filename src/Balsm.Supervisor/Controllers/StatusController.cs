using Balsm.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/status")]
public class AdminStatusController(ServerStatusService statusService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(statusService.GetStatus());
}
