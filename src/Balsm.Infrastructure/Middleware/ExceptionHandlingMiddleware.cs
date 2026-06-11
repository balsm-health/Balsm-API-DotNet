using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Sentry;

namespace Balsm.Infrastructure.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString();
            logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

            // Capture in Sentry with correlation context
            var eventId = SentrySdk.CaptureException(ex, scope =>
            {
                scope.SetTag("correlationId", correlationId ?? "unknown");
                scope.SetTag("path", context.Request.Path.Value ?? "");
                scope.SetTag("method", context.Request.Method);
                scope.AddBreadcrumb($"Unhandled exception in {nameof(ExceptionHandlingMiddleware)}");
            });

            await HandleExceptionAsync(context, ex, correlationId, eventId.ToString());
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, string? correlationId, string? sentryEventId)
    {
        var (statusCode, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = $"https://httpstatuses.io/{statusCode}",
            Extensions = 
            { 
                ["correlationId"] = correlationId,
                ["sentry"] = sentryEventId 
            }
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}
