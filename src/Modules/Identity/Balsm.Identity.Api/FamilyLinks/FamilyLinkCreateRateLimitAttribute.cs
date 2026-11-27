using System.Security.Claims;
using Balsm.Infrastructure.RateLimit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Balsm.Identity.Api.FamilyLinks;

/// <summary>
/// Per-account fixed-window limit on family-link requests: bounds how fast one
/// account can probe jtis or spam requests. Keyed on the token's sub claim, so
/// it holds across devices and addresses. Uses the shared
/// <see cref="IRateLimitStore"/> (in-memory in Standalone, Redis in Cloud).
/// Runs as a resource filter — before model binding, so a refused request
/// costs no work.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FamilyLinkCreateRateLimitAttribute : Attribute, IAsyncResourceFilter
{
    public const int Limit = 20;
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
        var store = http.RequestServices.GetRequiredService<IRateLimitStore>();

        var decision = await store
            .TryConsumeAsync($"family-link:create:{userId}", Limit, Window, http.RequestAborted)
            .ConfigureAwait(false);

        if (!decision.Allowed)
        {
            http.RequestServices.GetService<ILogger<FamilyLinkCreateRateLimitAttribute>>()
                ?.LogWarning("Family link create rate limit hit. {UserId} {Module} {Action}", userId, "Identity", "CreateFamilyLink");
            http.Response.Headers.RetryAfter = decision.RetryAfterSeconds.ToString();
            context.Result = new ObjectResult(new FamilyLinkErrorResponse(new FamilyLinkError( // sync-ok: filter result, not Task.Result
                "RateLimitExceeded", "Too many family-link requests; try again later.", http.Items["CorrelationId"] as string)))
            {
                StatusCode = StatusCodes.Status429TooManyRequests,
            };
            return;
        }

        await next().ConfigureAwait(false);
    }
}
