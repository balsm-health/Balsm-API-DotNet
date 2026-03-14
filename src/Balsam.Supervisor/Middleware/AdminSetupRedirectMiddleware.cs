using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.Supervisor.Middleware;

public sealed class AdminSetupRedirectMiddleware
{
    private readonly RequestDelegate _next;

    public AdminSetupRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // With the React SPA, all /admin/* HTML navigation is handled client-side.
        // This middleware only guards the setup API endpoint (/api/v1/admin/auth/setup).
        if (path.Equals("/api/v1/admin/auth/setup", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(context.Request.Method))
        {
            var authService = context.RequestServices
                .GetRequiredService<Auth.AdminAuthService>();

            if (await authService.IsSetupCompleteAsync(context.RequestAborted))
            {
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"Setup already completed\"}");
                return;
            }
        }

        await _next(context);
    }
}
