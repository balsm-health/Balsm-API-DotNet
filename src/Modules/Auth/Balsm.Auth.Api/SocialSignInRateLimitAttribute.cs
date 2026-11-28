using Balsm.Infrastructure.RateLimit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Balsm.Auth.Api;

/// <summary>
/// Per-IP fixed-window rate limit for the social sign-in exchanges
/// (<c>POST /auth/google</c>, <c>POST /auth/apple</c>).
///
/// The endpoints are anonymous and each call makes the server validate a
/// third-party token, so the client IP is the only key there is. As an MVC
/// resource filter it runs before the action, including the
/// <c>SocialSignIn:Enabled</c> gate: a disabled endpoint is still bounded.
/// The limit is loose enough for carrier-NAT addresses shared by many patients.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SocialSignInRateLimitAttribute : Attribute, IAsyncResourceFilter
{
    public const int Limit = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var store = http.RequestServices.GetRequiredService<IRateLimitStore>();
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var decision = await store
            .TryConsumeAsync($"auth:social:ip:{ip}", Limit, Window, http.RequestAborted)
            .ConfigureAwait(false);

        if (!decision.Allowed)
        {
            http.RequestServices.GetService<ILogger<SocialSignInRateLimitAttribute>>()
                ?.LogWarning("Social sign-in IP rate limit hit");
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
