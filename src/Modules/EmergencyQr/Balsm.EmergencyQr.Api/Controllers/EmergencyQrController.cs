using Balsm.EmergencyQr.Application.Commands;
using Balsm.EmergencyQr.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Balsm.EmergencyQr.Api.Controllers;

[ApiController]
[Route("emergency-qr")]
public sealed class EmergencyQrController(IMediator mediator) : ControllerBase
{
    /// <summary>Public-envelope schema version (spec v2.0).</summary>
    private const int EnvelopeVersion = 1;

    private Guid CurrentUserId => Guid.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? Guid.Empty.ToString());

    // POST /emergency-qr/mint  (T119, FR-013/014, AgeGate)
    [HttpPost("mint")]
    [Authorize]
    public async Task<IActionResult> Mint([FromBody] MintRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new MintEmergencyQrCommand(CurrentUserId, req.Ciphertext, req.ProfileEtag, req.TtlSeconds, req.TokenId), ct);
        return Ok(new { data = new { token_id = result.TokenId, expires_at = result.ExpiresAt } });
    }

    // POST /emergency-qr/{jti}/revoke  (T120, FR-015/034, SelfOnly)
    [HttpPost("{jti:guid}/revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke(Guid jti, CancellationToken ct)
    {
        await mediator.Send(new RevokeEmergencyQrCommand(jti, CurrentUserId), ct);
        return Ok(new { data = new { revoked = true } });
    }

    // PUT /emergency-qr/{jti}/ciphertext  (permanent QR data refresh, SelfOnly)
    // Replaces the encrypted snapshot in place so a permanent QR's URL stays
    // stable while a scan always shows current data. Server sees ciphertext only.
    [HttpPut("{jti:guid}/ciphertext")]
    [Authorize]
    public async Task<IActionResult> UpdateCiphertext(Guid jti, [FromBody] UpdateCiphertextRequest req, CancellationToken ct)
    {
        await mediator.Send(
            new UpdateEmergencyQrCiphertextCommand(jti, CurrentUserId, req.Ciphertext, req.ProfileEtag), ct);
        return Ok(new { data = new { updated = true } });
    }

    // GET /emergency-qr/resolve/{jti}  (T121, FR-015/216, no auth)
    // Spec v2.0: revoked, expired, and unknown are a UNIFORM 404 — a distinct
    // answer would confirm to a prober that a jti once existed.
    [HttpGet("resolve/{jti:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Resolve(Guid jti, CancellationToken ct)
    {
        var result = await mediator.Send(new ResolveEmergencyQrQuery(jti, ClassifyClient()), ct);
        if (result is null)
            return NotFound(new { error = new { code = "NotFound" } });

        Response.Headers.CacheControl = "no-store";
        return Ok(new
        {
            data = new
            {
                v = EnvelopeVersion,
                type = result.Type,
                expires_at = result.ExpiresAt,
                ciphertext_base64 = Convert.ToBase64String(result.Ciphertext)
            }
        });
    }

    // GET /emergency-qr/scans  (spec v2.0 scan history, SelfOnly)
    [HttpGet("scans")]
    [Authorize]
    public async Task<IActionResult> GetScans(CancellationToken ct)
    {
        var scans = await mediator.Send(new GetQrScansQuery(CurrentUserId), ct);
        return Ok(new
        {
            data = new
            {
                scans = scans.Select(s => new
                {
                    token_id = s.TokenId,
                    resolved_at = s.ResolvedAt,
                    client = s.ClientClass,
                    country = s.Country
                })
            }
        });
    }

    // GET /emergency-qr/active  (T121a, FR-014, SelfOnly)
    [HttpGet("active")]
    [Authorize]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var result = await mediator.Send(new GetActiveQrQuery(CurrentUserId), ct);
        if (result is null)
            return Ok(new { data = (object?)null });

        return Ok(new { data = new { token_id = result.TokenId, expires_at = result.ExpiresAt, ttl_seconds = result.TtlSeconds } });
    }

    /// <summary>Coarse scanner class for scan history: "app" (Dart client),
    /// "web" (any browser), else "unknown". The raw User-Agent never persists.</summary>
    private string ClassifyClient()
    {
        var ua = Request.Headers.UserAgent.ToString();
        if (ua.Contains("Dart", StringComparison.OrdinalIgnoreCase)) return "app";
        if (ua.Contains("Mozilla", StringComparison.OrdinalIgnoreCase)) return "web";
        return "unknown";
    }
}

public sealed record MintRequest(byte[] Ciphertext, string ProfileEtag, int TtlSeconds, Guid? TokenId = null);
public sealed record UpdateCiphertextRequest(byte[] Ciphertext, string ProfileEtag);
