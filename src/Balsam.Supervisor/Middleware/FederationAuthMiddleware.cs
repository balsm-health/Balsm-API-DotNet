using Balsam.Supervisor.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.Supervisor.Middleware;

public sealed class FederationAuthMiddleware
{
    private readonly RequestDelegate _next;
    internal const string ApiKeyHeader = "X-Balsam-ApiKey";

    private static readonly string[] PublicPaths =
    [
        "/api/v1/federation/pair"
    ];

    public FederationAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only intercept /api/v1/federation/ routes (NOT /api/v1/admin/federation/)
        if (!path.StartsWith("/api/v1/federation/", StringComparison.OrdinalIgnoreCase))
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

        // Validate API key
        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyValues)
            || string.IsNullOrEmpty(apiKeyValues.FirstOrDefault()))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(
                new { message = "API key required" });
            return;
        }

        var federationService = context.RequestServices
            .GetRequiredService<FederationService>();
        var pairing = await federationService.ValidateApiKeyAsync(apiKeyValues.First()!);

        if (pairing is null)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(
                new { message = "Invalid API key" });
            return;
        }

        // Store validated pairing for downstream use
        context.Items["FederationPairing"] = pairing;
        await _next(context);
    }
}
