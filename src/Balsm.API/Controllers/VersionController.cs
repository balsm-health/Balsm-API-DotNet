using Balsm.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class VersionController(IConfiguration configuration, IHostEnvironment hostEnvironment) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        Version = typeof(VersionController).Assembly.GetName().Version?.ToString() ?? "0.1.0",
        DeploymentMode = configuration["DeploymentMode"] ?? "Unknown",
        Environment = hostEnvironment.GetDeploymentEnvironment(),
        Timestamp = DateTime.UtcNow
    });
}
