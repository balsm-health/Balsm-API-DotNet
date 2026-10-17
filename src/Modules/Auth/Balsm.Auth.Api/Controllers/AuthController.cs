using Balsm.Auth.Application.Commands;
using Balsm.Auth.Domain;
using Balsm.Geofence.Domain;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
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
        // "register" is still accepted on the wire: clients in the field send it,
        // and it now means the same thing Continue does — send a code, say
        // nothing about whether the address is known.
        if (OtpPurposeWire.Parse(req.Purpose) is not { } purpose)
            return BadRequest(new { error = new { code = "InvalidPurpose" } });

        // The email is written in the language the app is in. Clients that do
        // not say fall back to the browser's Accept-Language, then English.
        var language = !string.IsNullOrWhiteSpace(req.Language)
            ? req.Language
            : Request.Headers.AcceptLanguage.ToString().Split(',')[0];

        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await mediator.Send(
                new RequestOtpCommand(req.Email, req.CountryCode, req.CaptchaToken, purpose, ip, language), ct);
            return Ok(new { data = new { expires_in_seconds = result.ExpiresInSeconds } });
        }
        catch (OtpRateLimitException ex)
        {
            Response.Headers["Retry-After"] = ex.RetryAfterSeconds.ToString();
            return StatusCode(429, new { error = new { code = "RateLimitExceeded" } });
        }
        catch (AccountLockedException ex)
        {
            return Locked(ex);
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
    [ProducesResponseType(typeof(VerifyOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        AuthTokenResult result;
        try
        {
            result = await mediator.Send(
                new VerifyOtpCommand(req.Email, req.Code, req.DeviceId, req.DeviceLabel, req.ClientAccountId), ct);
        }
        catch (AccountLockedException ex)
        {
            return Locked(ex);
        }
        return Ok(new VerifyOtpResponse(new VerifyOtpTokens(
            result.AccessToken, result.RefreshToken, result.UserId, result.IsNewUser, result.AdoptedClientId)));
    }

    // GET /auth/otp/link?t=... — the emailed magic-link target. Carries the raw
    // link token into the app; no validation here (the app POSTs the token to
    // /auth/otp/verify-link). With no Otp:WebAppUrl configured it just 302s to
    // the native custom scheme. When a Flutter-web app URL IS configured it
    // serves an interstitial that tries the native app first and falls back to
    // the web app — so the SAME link works on device and in a browser.
    [HttpGet("otp/link")]
    [AllowAnonymous]
    public IActionResult GetOtpLink([FromQuery(Name = "t")] string t, [FromQuery(Name = "p")] string? p = null)
    {
        var scheme = configuration["Otp:AppLinkScheme"] ?? "balsm";
        // `p` is passed on only when it is one of the purposes we issue: this
        // endpoint is anonymous, and it writes its input into a redirect.
        var query = $"t={Uri.EscapeDataString(t)}";
        if (OtpPurposeWire.NormalizeLinkCode(p) is { } linkCode) query += $"&p={linkCode}";
        var nativeUrl = $"{scheme}://auth/link?{query}";
        var webAppUrl = configuration["Otp:WebAppUrl"];

        if (string.IsNullOrWhiteSpace(webAppUrl))
            return Redirect(nativeUrl);

        var webUrl = $"{webAppUrl.TrimEnd('/')}/auth/link?{query}";
        var nativeJs = System.Text.Json.JsonSerializer.Serialize(nativeUrl);
        var webJs = System.Text.Json.JsonSerializer.Serialize(webUrl);
        var html = $$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Balsm — Signing in…</title></head>
            <body style="font-family:system-ui,sans-serif;text-align:center;padding:2.5rem;color:#14202b">
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
        Balsm.SharedKernel.Results.Result<AuthTokenResult> result;
        try
        {
            result = await mediator.Send(
                new PasswordSignInCommand(req.Email, req.Password, req.DeviceId, req.DeviceLabel), ct);
        }
        catch (AccountLockedException ex)
        {
            return Locked(ex);
        }
        if (result.IsFailure)
            return Unauthorized(new { error = new { code = result.Error!.Code, message = result.Error.Message } });
        var tokens = result.Value!;
        return Ok(new
        {
            data = new
            {
                access_token = tokens.AccessToken,
                refresh_token = tokens.RefreshToken,
                user_id = tokens.UserId,
                is_new_user = tokens.IsNewUser,
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
        try
        {
            await mediator.Send(new ResetPasswordCommand(req.Email, req.Code, req.NewPassword), ct);
        }
        catch (AccountLockedException ex)
        {
            return Locked(ex);
        }
        return Ok(new { data = new { password_reset = true } });
    }

    // 423 with Retry-After: too many failed attempts for this address, from a
    // code request, code verify, password reset or password sign-in. The apps
    // count down from Retry-After.
    private ObjectResult Locked(AccountLockedException ex)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling((ex.LockedUntil - DateTime.UtcNow).TotalSeconds));
        Response.Headers["Retry-After"] = seconds.ToString();
        return StatusCode(423, new { error = new { code = "AccountLocked", message = AuthErrors.LockedMessage(seconds) } });
    }
}

/// <param name="Language">The app's language (<c>ar</c>, <c>en</c>, <c>ar-EG</c>); picks the email's language.</param>
public sealed record RequestOtpRequest(
    string Email, string CountryCode, string? CaptchaToken, string Purpose, string? Language = null);
public sealed record OidcRequest(string IdToken, Guid DeviceId, string DeviceLabel, string CountryCode);
public sealed record VerifyOtpRequest(
    string Email, string Code, Guid DeviceId, string DeviceLabel,
    [property: Description("Offline-created account id (UUIDv7). Adopted only for a new email when unused.")]
    Guid? ClientAccountId = null);
/// <summary>Body of a successful <c>POST /auth/otp/verify</c>.</summary>
public sealed record VerifyOtpResponse(VerifyOtpTokens Data);

/// <summary>Session for the verified email. <c>adopted_client_id</c> is false for an
/// existing email, when no id was sent, or when the id was not free.</summary>
public sealed record VerifyOtpTokens(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    [property: Description("True when this verify created the account.")]
    bool IsNewUser,
    [property: Description("True when the account was created under the request's client_account_id.")]
    bool AdoptedClientId);

public sealed record VerifyLinkRequest(string Token, Guid DeviceId, string DeviceLabel);
public sealed record RefreshRequest(string RefreshToken, Guid DeviceId);
public sealed record SignOutRequest(Guid DeviceId);
public sealed record RecoveryClaimRequest(string Email, string SupportToken, Guid DeviceId, string DeviceLabel);
public sealed record PasswordSignInRequest(string Email, string Password, Guid DeviceId, string DeviceLabel);
public sealed record SetPasswordRequest(string Password);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
