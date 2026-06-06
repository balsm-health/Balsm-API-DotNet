using Balsm.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Supervisor.Controllers;

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

        // Report the hostname the mDNS service actually advertises (balsm-<slug>.local),
        // not the static default — MdnsName is the single source of truth.
        if (mdnsService.MdnsName is { Length: > 0 } name)
        {
            info.MdnsHostname = $"{name}.local";
            info.MdnsApiUrl = $"http://{name}.local:{port}";
        }

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
