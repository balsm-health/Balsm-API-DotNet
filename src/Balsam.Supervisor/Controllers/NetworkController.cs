using Balsam.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsam.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/network")]
public class AdminNetworkController(
    NetworkDiscoveryService networkDiscovery,
    MdnsService mdnsService,
    ConnectionInfoService connectionInfo) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var port = HttpContext.Request.Host.Port ?? 5050;
        var info = networkDiscovery.GetNetworkInfo(port);
        info.MdnsRegistered = mdnsService.IsRegistered;
        return Ok(info);
    }

    [HttpGet("connection-info")]
    public async Task<IActionResult> GetConnectionInfo()
    {
        var content = await connectionInfo.ReadConnectionInfoAsync();
        return content is not null
            ? Ok(new { Content = content })
            : NotFound(new { Message = "connection-info.txt has not been generated yet" });
    }
}
