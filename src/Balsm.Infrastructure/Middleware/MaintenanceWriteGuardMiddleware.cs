using System.Globalization;
using Balsm.Infrastructure.Maintenance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Balsm.Infrastructure.Middleware;

/// <summary>
/// While maintenance mode is <c>block</c>, refuses state-changing requests
/// (POST/PUT/PATCH/DELETE) with <c>503</c> + <c>Retry-After</c>, so writes are not
/// half-applied while operators work on the data stores. Reads are never refused:
/// health probes, <c>/status</c>, the maintenance read itself and the public
/// emergency-QR scan must stay reachable.
///
/// <c>readonly</c> is deliberately NOT enforced here: it is a client-side UX
/// mode (the app pauses writes), and an app build that predates maintenance mode
/// should keep working against a server that is still healthy.
/// </summary>
public sealed class MaintenanceWriteGuardMiddleware(RequestDelegate next, IMaintenanceModeService maintenance)
{
    /// <summary>Error code in the 503 problem body; clients match on it.</summary>
    public const string ErrorCode = "maintenance.blocked";

    // Writes that stay open in block mode:
    //   /api/v1/admin   — the super admin must be able to switch maintenance off again
    //   /api/v1/federation, /connect — server-to-server pairing, not patient data
    //   /auth/refresh, /auth/sign-out — keep existing sessions alive / let a patient sign out
    private static readonly string[] AllowedWritePrefixes =
    [
        "/api/v1/admin",
        "/api/v1/federation",
        "/connect",
        "/auth/refresh",
        "/auth/sign-out",
    ];

    private const int RetryAfterSeconds = 300;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsWrite(context.Request.Method) || IsAllowed(context.Request.Path))
        {
            await next(context);
            return;
        }

        var state = await maintenance.GetAsync(context.RequestAborted);
        if (state.Mode != MaintenanceModes.Block)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Balsm is down for maintenance",
            Detail = "Changes are paused during maintenance. Try again later.",
            Type = "https://api.balsm.health/problems/maintenance-blocked",
        };
        problem.Extensions["code"] = ErrorCode;
        if (state.EndsAt is { } endsAt)
            problem.Extensions["ends_at"] = endsAt;
        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json", context.RequestAborted);
    }

    private static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static bool IsAllowed(PathString path) =>
        AllowedWritePrefixes.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
}
