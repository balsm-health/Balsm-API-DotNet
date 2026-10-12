using Balsm.Infrastructure.RateLimit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Balsm.CareDirectory.Api;

/// <summary>
/// Per-IP fixed-window rate limit for the public care directory.
///
/// The directory is anonymous by design, so the client IP is the only key
/// there is. This is load protection, not secrecy: the data is public and the
/// offline packs ship all of it anyway.
///
/// Runs as an MVC resource filter, so it only counts requests that reach the
/// controller. A response served by the output cache never gets here — those
/// cost nothing and stay unlimited; what is bounded is the work that hits the
/// database or the disk.
///
/// The limit is deliberately loose: mobile carriers put many patients behind
/// one address, and a map pan is several requests.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class CareDirectoryRateLimitAttribute : Attribute, IAsyncResourceFilter
{
    public const int Limit = 300;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var store = http.RequestServices.GetRequiredService<IRateLimitStore>();
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var decision = await store
            .TryConsumeAsync($"care:ip:{ip}", Limit, Window, http.RequestAborted)
            .ConfigureAwait(false);

        if (!decision.Allowed)
        {
            http.RequestServices.GetService<ILogger<CareDirectoryRateLimitAttribute>>()
                ?.LogWarning("Care directory IP rate limit hit for [ip]");
            http.Response.Headers.RetryAfter = decision.RetryAfterSeconds.ToString();
            context.Result = new ObjectResult(new { error = new { code = "RateLimitExceeded" } }) // sync-ok: filter result, not Task.Result
            {
                StatusCode = StatusCodes.Status429TooManyRequests,
            };
            return;
        }

        await next().ConfigureAwait(false);
    }
}
