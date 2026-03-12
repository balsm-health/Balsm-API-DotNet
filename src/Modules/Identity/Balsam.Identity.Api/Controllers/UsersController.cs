using Microsoft.AspNetCore.Mvc;

namespace Balsam.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping() =>
        Ok(new { Module = "Identity", Status = "OK", Timestamp = DateTime.UtcNow });
}
