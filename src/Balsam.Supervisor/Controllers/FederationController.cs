using Balsam.Supervisor.Configuration;
using Balsam.Supervisor.Models;
using Balsam.Supervisor.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Balsam.Supervisor.Controllers;

[ApiController]
[Route("api/v1/federation")]
public class FederationController(
    FederationService federationService,
    IOptions<SupervisorOptions> options) : ControllerBase
{
    private readonly SupervisorOptions _options = options.Value;

    [HttpPost("pair")]
    public async Task<IActionResult> AcceptPairing([FromBody] PairRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Code is required" });
        }

        if (string.IsNullOrWhiteSpace(request.ServerId) ||
            string.IsNullOrWhiteSpace(request.ServerUrl) ||
            string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { message = "ServerId, ServerUrl, and ApiKey are required" });
        }

        var result = await federationService.ValidateAndCompletePairingAsync(request);
        if (result is null)
        {
            return BadRequest(new { message = "Invalid or expired pairing code" });
        }

        return Ok(result);
    }

    [HttpGet("heartbeat")]
    public IActionResult Heartbeat()
    {
        var pairing = HttpContext.Items["FederationPairing"] as ServerPairing;
        if (pairing is not null)
        {
            _ = federationService.UpdateHeartbeatAsync(pairing.Id);
        }

        return Ok(new HeartbeatResponse
        {
            ServerId = _options.ServerId ?? "unknown",
            Status = "active",
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> ReceiveSync([FromBody] SyncBatch batch)
    {
        var pairing = HttpContext.Items["FederationPairing"] as ServerPairing;
        if (pairing is null)
        {
            return Forbid();
        }

        if (batch.SourceServerId != pairing.ServerId)
        {
            return BadRequest(new { message = "Source server ID mismatch" });
        }

        await federationService.UpdateSyncTimestampAsync(pairing.Id);

        // For MVP, acknowledge receipt. SyncService will process inbound data.
        return Accepted(new
        {
            message = "Sync batch received",
            sequenceNumber = batch.SequenceNumber,
            recordCount = batch.Records.Count
        });
    }
}
