using Balsam.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsam.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/network")]
public class AdminNetworkController(
    NetworkDiscoveryService networkDiscovery,
    MdnsService mdnsService,
    ConnectionInfoService connectionInfo,
    ServerStatusService statusService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var port = HttpContext.Request.Host.Port ?? 5050;
        var info = networkDiscovery.GetNetworkInfo(port);
        info.MdnsRegistered = mdnsService.IsRegistered;

        var status = statusService.GetStatus();
        if (status.Mode == "public")
        {
            info.PublicIp = await networkDiscovery.GetPublicIpAsync();
        }

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
