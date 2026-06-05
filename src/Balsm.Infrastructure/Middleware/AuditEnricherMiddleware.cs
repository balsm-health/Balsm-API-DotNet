using Balsm.Infrastructure.Audit;
using Microsoft.AspNetCore.Http;

namespace Balsm.Infrastructure.Middleware;

public sealed class AuditEnricherMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Items["CorrelationId"] as string
            ?? context.Request.Headers["X-Correlation-Id"].FirstOrDefault();

        var actor = context.User?.Identity?.IsAuthenticated == true
            ? context.User.Identity.Name
            : null;

        var sourceIp = context.Connection.RemoteIpAddress?.ToString();

        AuditContext.Current = new AuditContextValue(actor, sourceIp, correlationId);

        await next(context);
    }
}
