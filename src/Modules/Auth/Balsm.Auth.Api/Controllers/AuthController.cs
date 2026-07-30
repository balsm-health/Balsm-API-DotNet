using Balsm.Auth.Application.Commands;
using Balsm.Geofence.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace Balsm.Auth.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    // POST /auth/otp/request  (T071)
    [HttpPost("otp/request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest req, CancellationToken ct)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await mediator.Send(
                new RequestOtpCommand(req.Email, req.CountryCode, req.CaptchaToken, ip), ct);
            return Ok(new { data = new { expires_in_seconds = result.ExpiresInSeconds } });
        }
        catch (OtpRateLimitException ex)
        {
            Response.Headers["Retry-After"] = ex.RetryAfterSeconds.ToString();
            return StatusCode(429, new { error = new { code = "RateLimitExceeded" } });
        }
        catch (AccountLockedException ex)
        {
            Response.Headers["Retry-After"] = ((int)(ex.LockedUntil - DateTime.UtcNow).TotalSeconds).ToString();
            return StatusCode(423, new { error = new { code = "AccountLocked" } });
        }
        catch (GeofenceDeniedException)
        {
            return StatusCode(403, new { error = new { code = "CountryDenied" } });
        }
    }

    // POST /auth/google  (T073b)
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> Google([FromBody] OidcRequest req, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new ExchangeGoogleTokenCommand(req.IdToken, req.DeviceId, req.DeviceLabel, req.CountryCode), ct);
            return Ok(new { data = new { access_token = result.AccessToken, refresh_token = result.RefreshToken, user_id = result.UserId, is_new_user = result.IsNewUser } });
        }
        catch (GeofenceDeniedException)
        {
            return StatusCode(403, new { error = new { code = "CountryDenied" } });
        }
    }

    // POST /auth/apple  (T073b)
    [HttpPost("apple")]
    [AllowAnonymous]
    public async Task<IActionResult> Apple([FromBody] OidcRequest req, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new ExchangeAppleTokenCommand(req.IdToken, req.DeviceId, req.DeviceLabel, req.CountryCode), ct);
            return Ok(new { data = new { access_token = result.AccessToken, refresh_token = result.RefreshToken, user_id = result.UserId, is_new_user = result.IsNewUser } });
        }
        catch (GeofenceDeniedException)
        {
            return StatusCode(403, new { error = new { code = "CountryDenied" } });
        }
    }

    // POST /auth/otp/verify  (T073a)
    [HttpPost("otp/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new VerifyOtpCommand(req.Email, req.Code, req.DeviceId, req.DeviceLabel), ct);
        return Ok(new
        {
            data = new
            {
                access_token = result.AccessToken,
                refresh_token = result.RefreshToken,
                user_id = result.UserId,
                is_new_user = result.IsNewUser
            }
        });
    }

    // GET /auth/otp/link?t=... — the emailed magic-link target. Carries the raw
    // link token into the app; no validation here (the app POSTs the token to
    // /auth/otp/verify-link). With no Otp:WebAppUrl configured it just 302s to
    // the native custom scheme. When a Flutter-web app URL IS configured it
    // serves an interstitial that tries the native app first and falls back to
    // the web app — so the SAME link works on device and in a browser.
    [HttpGet("otp/link")]
    [AllowAnonymous]
    public IActionResult GetOtpLink([FromQuery(Name = "t")] string t)
    {
        var scheme = configuration["Otp:AppLinkScheme"] ?? "balsm";
        var nativeUrl = $"{scheme}://auth/link?t={Uri.EscapeDataString(t)}";
        var webAppUrl = configuration["Otp:WebAppUrl"];

        if (string.IsNullOrWhiteSpace(webAppUrl))
            return Redirect(nativeUrl);

        var webUrl = $"{webAppUrl.TrimEnd('/')}/auth/link?t={Uri.EscapeDataString(t)}";
        var nativeJs = System.Text.Json.JsonSerializer.Serialize(nativeUrl);
        var webJs = System.Text.Json.JsonSerializer.Serialize(webUrl);
        var html = $$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Balsm — Signing in…</title></head>
            <body style="font-family:system-ui,sans-serif;text-align:center;padding:2.5rem;color:#2b2b25">
            <p>Signing you in…</p>
            <p><a id="cont" href={{webJs}}>Continue in browser</a></p>
            <script>
              var native = {{nativeJs}}, web = {{webJs}};
              // Try the installed app first; fall back to the web app shortly
              // after — unless the page gets hidden (the native app took over).
              var timer = setTimeout(function () { window.location.href = web; }, 1200);
              document.addEventListener('visibilitychange', function () {
                if (document.hidden) clearTimeout(timer);
              });
              window.location.href = native;
            </script></body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }

    // POST /auth/otp/verify-link — magic-link counterpart of /auth/otp/verify.
    // Runs the same session issuance and returns the same envelope shape.
    [HttpPost("otp/verify-link")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtpLink([FromBody] VerifyLinkRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new VerifyLinkCommand(req.Token, req.DeviceId, req.DeviceLabel), ct);
        return Ok(new
        {
            data = new
            {
                access_token = result.AccessToken,
                refresh_token = result.RefreshToken,
                user_id = result.UserId,
                is_new_user = result.IsNewUser
            }
        });
    }

    // POST /auth/refresh  (T073c)
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(new RefreshTokenCommand(req.RefreshToken, req.DeviceId), ct);
            return Ok(new { data = new { access_token = result.AccessToken, refresh_token = result.RefreshToken } });
        }
        catch (Exception ex) when (ex.Message is "TokenRevoked" or "TokenNotFound")
        {
            return Unauthorized(new { error = new { code = ex.Message } });
        }
    }

    // POST /auth/sign-out  (T073c)
    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOut([FromBody] SignOutRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub") ?? Guid.Empty.ToString());
        await mediator.Send(new SignOutCommand(userId, req.DeviceId), ct);
        return Ok(new { data = new { signed_out = true } });
    }

    // POST /auth/recovery/claim  (T035bd)
    [HttpPost("recovery/claim")]
    [AllowAnonymous]
    public async Task<IActionResult> RecoveryClaim([FromBody] RecoveryClaimRequest req, CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new RecoveryClaimCommand(req.Email, req.SupportToken, req.DeviceId, req.DeviceLabel), ct);
            return Ok(new { data = new { access_token = result.AccessToken, refresh_token = result.RefreshToken, user_id = result.UserId } });
        }
        catch (InvalidSupportTokenException)
        {
            return StatusCode(403, new { error = new { code = "InvalidSupportToken" } });
        }
        catch (RecoveryIdentityNotFoundException)
        {
            return NotFound(new { error = new { code = "IdentityNotFound" } });
        }
    }

    // POST /auth/password/sign-in — email + password sign-in for returning users.
    [HttpPost("password/sign-in")]
    [AllowAnonymous]
    public async Task<IActionResult> PasswordSignIn([FromBody] PasswordSignInRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new PasswordSignInCommand(req.Email, req.Password, req.DeviceId, req.DeviceLabel), ct);
        return Ok(new
        {
            data = new
            {
                access_token = result.AccessToken,
                refresh_token = result.RefreshToken,
                user_id = result.UserId,
                is_new_user = result.IsNewUser,
            }
        });
    }

    // POST /auth/password — set or change the signed-in user's password.
    [HttpPost("password")]
    [Authorize]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest req, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub") ?? Guid.Empty.ToString());
        await mediator.Send(new SetPasswordCommand(userId, req.Password), ct);
        return Ok(new { data = new { password_set = true } });
    }

    // POST /auth/password/reset — reset a forgotten password with the emailed
    // OTP code (send it first via POST /auth/otp/request).
    [HttpPost("password/reset")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        await mediator.Send(new ResetPasswordCommand(req.Email, req.Code, req.NewPassword), ct);
        return Ok(new { data = new { password_reset = true } });
    }
}

public sealed record RequestOtpRequest(string Email, string CountryCode, string? CaptchaToken);
public sealed record OidcRequest(string IdToken, Guid DeviceId, string DeviceLabel, string CountryCode);
public sealed record VerifyOtpRequest(string Email, string Code, Guid DeviceId, string DeviceLabel);
public sealed record VerifyLinkRequest(string Token, Guid DeviceId, string DeviceLabel);
public sealed record RefreshRequest(string RefreshToken, Guid DeviceId);
public sealed record SignOutRequest(Guid DeviceId);
public sealed record RecoveryClaimRequest(string Email, string SupportToken, Guid DeviceId, string DeviceLabel);
public sealed record PasswordSignInRequest(string Email, string Password, Guid DeviceId, string DeviceLabel);
public sealed record SetPasswordRequest(string Password);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
