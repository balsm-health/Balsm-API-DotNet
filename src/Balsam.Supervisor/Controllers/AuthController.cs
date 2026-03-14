using Balsam.Supervisor.Auth;
using Balsam.Supervisor.Middleware;
using Balsam.Supervisor.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Balsam.Supervisor.Controllers;

[ApiController]
[Route("api/v1/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly AdminAuthService _authService;
    private readonly AdminSessionService _sessionService;

    public AdminAuthController(
        AdminAuthService authService,
        AdminSessionService sessionService)
    {
        _authService = authService;
        _sessionService = sessionService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var isSetup = await _authService.IsSetupCompleteAsync();
        return Ok(new { setupComplete = isSetup });
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request)
    {
        if (await _authService.IsSetupCompleteAsync())
            return StatusCode(403, new { message = "Setup already completed" });

        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required" });

        if (request.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters" });

        await _authService.SetupAsync(request.Username, request.Password);

        var token = _sessionService.CreateSession(request.Username);
        SetSessionCookie(token);

        return Ok(new { message = "Setup complete" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(
            request.Username, request.Password);

        if (result.IsLockedOut)
            return StatusCode(429, new
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

        Response.Cookies.Delete(AdminAuthMiddleware.SessionCookieName);
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
                MaxAge = TimeSpan.FromHours(8)
            });
    }
}
