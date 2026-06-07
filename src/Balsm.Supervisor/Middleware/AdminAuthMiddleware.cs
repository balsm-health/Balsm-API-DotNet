using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Supervisor.Middleware;

public sealed class AdminAuthMiddleware
{
    private readonly RequestDelegate _next;
    internal const string SessionCookieName = "balsm_admin_session";

    private static readonly string[] PublicPaths =
    [
        "/api/v1/admin/auth/setup",
        "/api/v1/admin/auth/login",
        "/api/v1/admin/auth/status",
        "/api/v1/admin/auth/recovery/use"
    ];

    public AdminAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only intercept /api/v1/admin/ API routes
        if (!path.StartsWith("/api/v1/admin/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Allow public endpoints through
        foreach (var publicPath in PublicPaths)
        {
            if (path.StartsWith(publicPath, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        var sessionService = context.RequestServices
            .GetRequiredService<Auth.AdminSessionService>();
        var authService = context.RequestServices
            .GetRequiredService<Auth.AdminAuthService>();

        // Local CLI loopback token bypasses session-cookie check
        if (context.Items.ContainsKey(LocalOsTrustMiddleware.LocalClaimKey))
        {
            await _next(context);
            return;
        }

        // If setup not complete, reject with 403
        if (!await authService.IsSetupCompleteAsync(context.RequestAborted))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(
                new { message = "Setup required", redirect = "/admin/setup" });
            return;
        }

        // Validate session cookie
        if (!context.Request.Cookies.TryGetValue(SessionCookieName, out var token)
            || string.IsNullOrEmpty(token)
            || !sessionService.ValidateSession(token))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(
                new { message = "Authentication required" });
            return;
        }

        await _next(context);
    }
}
