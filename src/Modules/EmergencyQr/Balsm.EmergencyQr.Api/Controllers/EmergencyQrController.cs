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
            new MintEmergencyQrCommand(CurrentUserId, req.Ciphertext, req.ProfileEtag, req.PreferredLanguage, req.TtlSeconds, req.TokenId), ct);
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
            new UpdateEmergencyQrCiphertextCommand(jti, CurrentUserId, req.Ciphertext, req.ProfileEtag, req.PreferredLanguage), ct);
        return Ok(new { data = new { updated = true } });
    }

    // GET /emergency-qr/resolve/{jti}  (T121, FR-015/216, no auth)
    [HttpGet("resolve/{jti:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Resolve(Guid jti, CancellationToken ct)
    {
        var result = await mediator.Send(new ResolveEmergencyQrQuery(jti), ct);
        if (result is null)
            return StatusCode(410, new { error = new { code = "TokenExpiredOrRevoked" } });

        return Ok(new
        {
            data = new
            {
                ciphertext = Convert.ToBase64String(result.Ciphertext),
                preferred_language = result.PreferredLanguage,
                expires_at = result.ExpiresAt
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
}

public sealed record MintRequest(byte[] Ciphertext, string ProfileEtag, string PreferredLanguage, int TtlSeconds, Guid? TokenId = null);
public sealed record UpdateCiphertextRequest(byte[] Ciphertext, string ProfileEtag, string PreferredLanguage);
