using Balsm.Supervisor.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Supervisor.Controllers;

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
            name = "Balsm Healthcare Platform",
            version = status.Version,
            apiUrl = $"{Request.Scheme}://{Request.Host}/api/v1"
        });
    }
}
