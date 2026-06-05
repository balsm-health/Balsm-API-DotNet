using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Balsm.Supervisor.Middleware;

public sealed class RateLimitMiddleware(RequestDelegate next, IMemoryCache cache)
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public async Task InvokeAsync(HttpContext context)
    {
        string email = string.Empty;

        if (context.Request.ContentType?.Contains("application/json") == true
            && context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            try
            {
                var doc = await JsonDocument.ParseAsync(context.Request.Body).ConfigureAwait(false);
                context.Request.Body.Position = 0;
                if (doc.RootElement.TryGetProperty("username", out var u))
                    email = u.GetString() ?? string.Empty;
            }
            catch
            {
                context.Request.Body.Position = 0;
            }
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"ratelimit:{email.ToLowerInvariant()}:{ip}";

        var attempts = cache.GetOrCreate(key, e =>
        {
            e.SlidingExpiration = Window;
            return 0;
        });

        if (attempts >= MaxAttempts)
        {
            context.Response.StatusCode = 429;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { message = "Too many attempts. Try again later." }))
                .ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);

        if (context.Response.StatusCode == 401 || context.Response.StatusCode == 423)
        {
            cache.Set(key, attempts + 1, new MemoryCacheEntryOptions
            {
                SlidingExpiration = Window
            });
        }
        else if (context.Response.StatusCode == 200)
        {
            cache.Remove(key);
        }
    }
}
