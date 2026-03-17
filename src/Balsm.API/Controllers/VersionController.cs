using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class VersionController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        Version = typeof(VersionController).Assembly.GetName().Version?.ToString() ?? "0.1.0",
        DeploymentMode = configuration["Balsm:DeploymentMode"] ?? "Unknown",
        Timestamp = DateTime.UtcNow
    });
}
