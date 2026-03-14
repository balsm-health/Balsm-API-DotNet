using Balsam.Supervisor.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Balsam.Supervisor.Controllers;

[ApiController]
[Route("connect")]
public class ConnectController(ServerStatusService statusService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var status = statusService.GetStatus();
        return Ok(new
        {
            name = "Balsam Healthcare Platform",
            version = status.Version,
            apiUrl = $"{Request.Scheme}://{Request.Host}/api/v1"
        });
    }
}
