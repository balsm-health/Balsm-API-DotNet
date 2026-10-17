using System.Text.Json;
using Balsm.Infrastructure.Lifecycle;
using Microsoft.AspNetCore.Http;

namespace Balsm.Infrastructure.Middleware;

public sealed class MigrationGateMiddleware(RequestDelegate next, ReadinessGate gate)
{
    private static readonly string[] PassThroughPaths =
    [
        "/api/v1/health",
        "/api/v1/server-info"
    ];

    // Metadata endpoints (no DB access) — must be reachable before migrations complete
    // so OpenAPI tooling and the Scalar UI work during cold boot and during recovery.
    private static readonly string[] PassThroughPrefixes =
    [
        "/openapi",
        "/scalar"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!gate.IsReady && !IsPassThrough(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { ready = false, reason = gate.Reason }));
            return;
        }

        await next(context);
    }

    private static bool IsPassThrough(PathString path) =>
        PassThroughPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase))
        || PassThroughPrefixes.Any(prefix =>
            path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
