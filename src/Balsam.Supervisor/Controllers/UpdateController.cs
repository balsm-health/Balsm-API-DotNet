using Balsam.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsam.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/update")]
public class AdminUpdateController(SelfUpdateService updateService) : ControllerBase
{
    [HttpGet("check")]
    public async Task<IActionResult> Check()
    {
        try
        {
            var info = await updateService.CheckForUpdateAsync();
            return Ok(info);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { Message = $"Failed to reach GitHub: {ex.Message}" });
        }
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply()
    {
        try
        {
            await updateService.ApplyUpdateAsync();
            return Ok(new { Message = "Update applied. The process will restart." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { Message = $"Download failed: {ex.Message}" });
        }
    }
}
