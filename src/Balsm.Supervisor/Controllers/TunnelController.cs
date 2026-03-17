using System.Net.Http.Json;
using System.Text.Json;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/tunnel")]
public class AdminTunnelController(
    CloudflareTunnelService tunnelService,
    IOptions<SupervisorOptions> options,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private readonly SupervisorOptions _options = options.Value;

    [HttpGet]
    public IActionResult GetStatus()
    {
        return Ok(BuildStatusResponse());
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register()
    {
        if (!tunnelService.IsCloudflaredInstalled)
        {
            return BadRequest(new { Message = "cloudflared is not installed on this server." });
        }

        // Generate or reuse server ID
        var serverId = _options.ServerId;
        if (string.IsNullOrEmpty(serverId))
        {
            serverId = Guid.NewGuid().ToString("N")[..12];
        }

        // Call the central registry
        var client = httpClientFactory.CreateClient("BalsmRegistry");
        var registryUrl = _options.RegistryUrl.TrimEnd('/');

        var request = new HttpRequestMessage(HttpMethod.Post, $"{registryUrl}/api/tunnels/register")
        {
            Content = JsonContent.Create(new { serverId })
        };

        if (!string.IsNullOrEmpty(_options.RegistrySecret))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.RegistrySecret);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { Message = $"Failed to reach registry: {ex.Message}" });
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode,
                new { Message = $"Registry error: {errorBody}" });
        }

        var registryResponse = await response.Content.ReadFromJsonAsync<TunnelRegistryResponse>();
        if (registryResponse is null)
        {
            return StatusCode(502, new { Message = "Invalid response from registry" });
        }

        // Save to config
        await SaveTunnelConfigAsync(serverId, registryResponse.TunnelToken, registryResponse.TunnelUrl);

        // Start the tunnel
        tunnelService.RegisteredUrl = registryResponse.TunnelUrl;
        var port = HttpContext.Request.Host.Port ?? 5050;
        await tunnelService.StartTunnelAsync("named", registryResponse.TunnelToken, port);

        return Ok(BuildStatusResponse());
    }

    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister()
    {
        var serverId = _options.ServerId;
        if (string.IsNullOrEmpty(serverId))
        {
            return BadRequest(new { Message = "No tunnel is registered." });
        }

        // Stop the tunnel process
        await tunnelService.StopTunnelAsync();
        tunnelService.RegisteredUrl = null;

        // Call the registry to delete
        var client = httpClientFactory.CreateClient("BalsmRegistry");
        var registryUrl = _options.RegistryUrl.TrimEnd('/');

        var request = new HttpRequestMessage(HttpMethod.Delete, $"{registryUrl}/api/tunnels/{serverId}");
        if (!string.IsNullOrEmpty(_options.RegistrySecret))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.RegistrySecret);
        }

        try
        {
            await client.SendAsync(request);
        }
        catch (Exception ex)
        {
            // Log but don't fail — the tunnel is already stopped locally
            return Ok(new { Message = $"Tunnel stopped. Registry cleanup warning: {ex.Message}" });
        }

        // Clear config
        await ClearTunnelConfigAsync();

        return Ok(BuildStatusResponse());
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        await tunnelService.StopTunnelAsync();
        return Ok(new { Message = "Tunnel stopped" });
    }

    // Keep manual token support for advanced users
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

        return Ok(BuildStatusResponse());
    }

    private TunnelStatusResponse BuildStatusResponse()
    {
        return new TunnelStatusResponse
        {
            IsRunning = tunnelService.IsRunning,
            TunnelUrl = tunnelService.TunnelUrl,
            TunnelType = tunnelService.TunnelType,
            Error = tunnelService.ErrorMessage,
            CloudflaredInstalled = tunnelService.IsCloudflaredInstalled,
            RegisteredUrl = tunnelService.RegisteredUrl,
            ServerId = _options.ServerId
        };
    }

    private static async Task SaveTunnelConfigAsync(string serverId, string tunnelToken, string tunnelUrl)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");

        var config = await ReadConfigAsync(configPath);
        var supervisor = GetOrCreateSection(config, "Supervisor");

        supervisor["EnableTunnel"] = true;
        supervisor["TunnelToken"] = tunnelToken;
        supervisor["TunnelUrl"] = tunnelUrl;
        supervisor["ServerId"] = serverId;
        config["Supervisor"] = supervisor;

        var json = JsonSerializer.Serialize(config,
            new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(configPath, json);
    }

    private static async Task ClearTunnelConfigAsync()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");

        var config = await ReadConfigAsync(configPath);
        var supervisor = GetOrCreateSection(config, "Supervisor");

        supervisor["EnableTunnel"] = false;
        supervisor.Remove("TunnelToken");
        supervisor.Remove("TunnelUrl");
        // Keep ServerId for reuse
        config["Supervisor"] = supervisor;

        var json = JsonSerializer.Serialize(config,
            new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(configPath, json);
    }

    private static async Task<Dictionary<string, object>> ReadConfigAsync(string path)
    {
        if (!System.IO.File.Exists(path))
            return new Dictionary<string, object>();

        var existing = await System.IO.File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(existing)
               ?? new Dictionary<string, object>();
    }

    private static Dictionary<string, object> GetOrCreateSection(
        Dictionary<string, object> config, string key)
    {
        if (config.TryGetValue(key, out var existing) && existing is JsonElement el)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(el.GetRawText())
                   ?? new Dictionary<string, object>();
        }

        return new Dictionary<string, object>();
    }
}
