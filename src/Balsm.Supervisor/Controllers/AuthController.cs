using Balsm.SharedKernel.Contracts;
using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Middleware;
using Balsm.Supervisor.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
namespace Balsm.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly AdminAuthService _authService;
    private readonly AdminSessionService _sessionService;
    private readonly RecoveryCodeService _recoveryCodeService;
    private readonly IFirstRunOrchestrator _firstRun;

    public AdminAuthController(
        AdminAuthService authService,
        AdminSessionService sessionService,
        RecoveryCodeService recoveryCodeService,
        IFirstRunOrchestrator firstRun)
    {
        _authService = authService;
        _sessionService = sessionService;
        _recoveryCodeService = recoveryCodeService;
        _firstRun = firstRun;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var isSetup = await _authService.IsSetupCompleteAsync();
        return Ok(new { setupComplete = isSetup });
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request, CancellationToken ct = default)
    {
        if (await _authService.IsSetupCompleteAsync())
            return StatusCode(403, new { message = "Setup already completed" });

        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required" });

        if (request.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters" });

        await _authService.SetupAsync(request.Username, request.Password);

        var workspaceName = request.WorkspaceName ?? "My Workspace";
        var workspaceSlug = request.WorkspaceSlug ?? "my-workspace";
        await _firstRun.SeedWorkspaceAsync(workspaceName, workspaceSlug, request.Locale ?? "en", ct).ConfigureAwait(false);

        var recoveryCode = await _recoveryCodeService.GenerateAsync(ct).ConfigureAwait(false);

        var token = _sessionService.CreateSession(request.Username);
        SetSessionCookie(token);

        return Ok(new { message = "Setup complete", recoveryCode });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(
            request.Username, request.Password);

        if (result.IsLockedOut)
            return StatusCode(423, new
            {
                message = result.ErrorMessage,
                lockoutRemaining = result.LockoutRemaining?.TotalSeconds
            });

        if (!result.IsSuccess)
            return Unauthorized(new { message = result.ErrorMessage });

        var token = _sessionService.CreateSession(request.Username);
        SetSessionCookie(token);

        return Ok(new { message = "Login successful" });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        if (Request.Cookies.TryGetValue(
            AdminAuthMiddleware.SessionCookieName, out var token))
        {
            _sessionService.InvalidateSession(token!);
        }

        Response.Cookies.Delete(AdminAuthMiddleware.SessionCookieName, new CookieOptions
        {
            Path = "/",
            Domain = ParentCookieDomain(Request.Host.Host)
        });
        return Ok(new { message = "Logged out" });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request)
    {
        try
        {
            await _authService.ChangePasswordAsync(
                request.CurrentPassword, request.NewPassword);

            _sessionService.InvalidateAllSessions();

            var token = _sessionService.CreateSession("admin");
            SetSessionCookie(token);

            return Ok(new { message = "Password changed successfully" });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Current password is incorrect" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private void SetSessionCookie(string token)
    {
        Response.Cookies.Append(
            AdminAuthMiddleware.SessionCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = Request.IsHttps,
                Path = "/",
                // Scope to the parent host (e.g. balsm.local) so the cookie is shared
                // between the admin panel (balsm.local) and the API (api.balsm.local).
                // Host-only (null) for localhost/IP where a Domain attribute is invalid.
                Domain = ParentCookieDomain(Request.Host.Host),
                MaxAge = TimeSpan.FromHours(8)
            });
    }

    private static string? ParentCookieDomain(string host)
    {
        if (!host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            && !host.EndsWith(".health", StringComparison.OrdinalIgnoreCase))
            return null; // localhost / IP — host-only cookie
        return host.StartsWith("api.", StringComparison.OrdinalIgnoreCase)
            ? host["api.".Length..]
            : host;
    }

    // ── Recovery code endpoints ───────────────────────────────────────────────

    /// <summary>POST /api/v1/admin/auth/recovery/use — consume recovery code and reset password.</summary>
    [HttpPost("recovery/use")]
    public async Task<IActionResult> UseRecoveryCode([FromBody] UseRecoveryCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RecoveryCode) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { message = "Recovery code and new password are required" });

        if (request.NewPassword.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters" });

        var valid = await _recoveryCodeService.ConsumeAsync(request.RecoveryCode);
        if (!valid)
            return Unauthorized(new { message = "Invalid or already used recovery code" });

        await _authService.ChangePasswordAsync_Internal(request.NewPassword);

        _sessionService.InvalidateAllSessions();
        var token = _sessionService.CreateSession("admin");
        SetSessionCookie(token);

        return Ok(new { message = "Password reset via recovery code" });
    }

    /// <summary>POST /api/v1/admin/auth/recovery/regenerate — invalidate old code and generate new one.</summary>
    [HttpPost("recovery/regenerate")]
    public async Task<IActionResult> RegenerateRecoveryCode()
    {
        await _recoveryCodeService.RetireAsync();
        var code = await _recoveryCodeService.GenerateAsync();
        return Ok(new { recoveryCode = code });
    }
}
