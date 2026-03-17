using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
}
