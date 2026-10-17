using System.Net.Http.Json;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/federation")]
public class AdminFederationController(
    FederationService federationService,
    IOptions<SupervisorOptions> options,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private readonly SupervisorOptions _options = options.Value;

    [HttpGet("pairings")]
    public async Task<IActionResult> GetPairings()
    {
        var pairings = await federationService.GetPairingsAsync();
        return Ok(new PairingListResponse { Pairings = pairings });
    }

    [HttpPost("pairings/generate-code")]
    public IActionResult GenerateCode()
    {
        var response = federationService.GenerateCode();
        return Ok(response);
    }

    [HttpPost("pairings/initiate")]
    public async Task<IActionResult> InitiatePairing([FromBody] InitiatePairingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ServerUrl) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "ServerUrl and Code are required" });
        }

        // Enforce HTTPS for security
        if (!request.ServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !request.ServerUrl.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) &&
            !request.ServerUrl.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "ServerUrl must use HTTPS (except for localhost)" });
        }

        var ourApiKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var ourApiKeyBase64 = Convert.ToBase64String(ourApiKey);

        var serverId = _options.ServerId ?? "unknown";
        var pairRequest = new PairRequest
        {
            Code = request.Code,
            ServerId = serverId,
            ServerName = Environment.MachineName,
            ServerUrl = GetOurServerUrl(),
            ApiKey = ourApiKeyBase64
        };

        // Call the remote server's federation pair endpoint
        var client = httpClientFactory.CreateClient("Federation");
        var remoteUrl = request.ServerUrl.TrimEnd('/');

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(
                $"{remoteUrl}/api/v1/federation/pair", pairRequest);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = $"Failed to reach remote server: {ex.Message}" });
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode,
                new { message = $"Remote server rejected pairing: {errorBody}" });
        }

        var pairResponse = await response.Content.ReadFromJsonAsync<PairResponse>();
        if (pairResponse is null)
        {
            return StatusCode(502, new { message = "Invalid response from remote server" });
        }

        // Store the pairing locally
        await federationService.InitiatePairingAsync(
            pairResponse.ServerId,
            pairResponse.ServerName,
            request.ServerUrl,
            pairResponse.ApiKey);

        return Ok(new { message = "Pairing established", remoteServerId = pairResponse.ServerId });
    }

    [HttpDelete("pairings/{id:guid}")]
    public async Task<IActionResult> RemovePairing(Guid id)
    {
        var removed = await federationService.RemovePairingAsync(id);
        if (!removed) return NotFound(new { message = "Pairing not found" });
        return Ok(new { message = "Pairing removed" });
    }

    [HttpPut("pairings/{id:guid}/pause")]
    public async Task<IActionResult> PausePairing(Guid id)
    {
        var paused = await federationService.PausePairingAsync(id);
        if (!paused) return NotFound(new { message = "Pairing not found" });
        return Ok(new { message = "Pairing paused" });
    }

    [HttpPut("pairings/{id:guid}/resume")]
    public async Task<IActionResult> ResumePairing(Guid id)
    {
        var resumed = await federationService.ResumePairingAsync(id);
        if (!resumed) return NotFound(new { message = "Pairing not found" });
        return Ok(new { message = "Pairing resumed" });
    }

    private string GetOurServerUrl()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}";
    }
}
