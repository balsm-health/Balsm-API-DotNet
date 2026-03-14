using System.Text.Json;
using Balsam.Supervisor.Models;
using Balsam.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;

namespace Balsam.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/tunnel")]
public class AdminTunnelController(
    CloudflareTunnelService tunnelService) : ControllerBase
{
    [HttpGet]
    public IActionResult GetStatus()
    {
        return Ok(new TunnelStatusResponse
        {
            IsRunning = tunnelService.IsRunning,
            TunnelUrl = tunnelService.TunnelUrl,
            TunnelType = tunnelService.TunnelType,
            Error = tunnelService.ErrorMessage,
            CloudflaredInstalled = tunnelService.IsCloudflaredInstalled
        });
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] TunnelStartRequest request)
    {
        if (request.Type is not "quick" and not "named")
        {
            return BadRequest(new { Message = "Type must be 'quick' or 'named'" });
        }

        if (request.Type == "named" && string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { Message = "Token is required for named tunnels" });
        }

        var port = HttpContext.Request.Host.Port ?? 5050;
        await tunnelService.StartTunnelAsync(request.Type, request.Token, port);

        return Ok(new TunnelStatusResponse
        {
            IsRunning = tunnelService.IsRunning,
            TunnelUrl = tunnelService.TunnelUrl,
            TunnelType = tunnelService.TunnelType,
            Error = tunnelService.ErrorMessage,
            CloudflaredInstalled = tunnelService.IsCloudflaredInstalled
        });
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        await tunnelService.StopTunnelAsync();
        return Ok(new { Message = "Tunnel stopped" });
    }

    [HttpPost("token")]
    public async Task<IActionResult> SaveToken([FromBody] TunnelTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { Message = "Token is required" });
        }

        await SaveTunnelTokenToConfigAsync(request.Token);
        return Ok(new { Message = "Token saved. Use Start with type 'named' to connect." });
    }

    private static async Task SaveTunnelTokenToConfigAsync(string token)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");

        Dictionary<string, object> config;

        if (System.IO.File.Exists(configPath))
        {
            var existing = await System.IO.File.ReadAllTextAsync(configPath);
            config = JsonSerializer.Deserialize<Dictionary<string, object>>(existing)
                     ?? new Dictionary<string, object>();
        }
        else
        {
            config = new Dictionary<string, object>();
        }

        // Read existing Supervisor section or create new one
        Dictionary<string, object> supervisor;
        if (config.TryGetValue("Supervisor", out var existing_sup) && existing_sup is JsonElement el)
        {
            supervisor = JsonSerializer.Deserialize<Dictionary<string, object>>(el.GetRawText())
                         ?? new Dictionary<string, object>();
        }
        else
        {
            supervisor = new Dictionary<string, object>();
        }

        supervisor["EnableTunnel"] = true;
        supervisor["TunnelToken"] = token;
        config["Supervisor"] = supervisor;

        var json = JsonSerializer.Serialize(config,
            new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(configPath, json);
    }
}
