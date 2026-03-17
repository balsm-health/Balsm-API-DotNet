using Microsoft.AspNetCore.Mvc;

namespace Balsm.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DummyController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        Message = "Dummy endpoint is working",
        Version = "1.0.0",
        Timestamp = DateTime.UtcNow
    });

    [HttpGet("{id:int}")]
    public IActionResult GetById(int id) => Ok(new
    {
        Id = id,
        Name = $"Dummy Item {id}",
        Timestamp = DateTime.UtcNow
    });
}
