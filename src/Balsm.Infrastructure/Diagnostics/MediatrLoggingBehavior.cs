using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Diagnostics;

/// <summary>
/// Traces every MediatR request (command/query) through the application layer:
/// name, elapsed time, and success/failure. This is the "process" half of debug
/// tracing — the HTTP middleware covers transport, this covers handling.
///
/// Emits at <see cref="LogLevel.Debug"/>, so it is silent in Production (default
/// minimum level Information) and visible in Development without any extra flag.
/// The <see cref="ILogger.IsEnabled"/> guard makes it truly zero-cost when Debug
/// is off. The request payload is deliberately NOT logged — commands carry PHI
/// and credentials (passwords, OTP codes, DOB); only the type name is safe.
/// </summary>
public sealed class MediatrLoggingBehavior<TRequest, TResponse>(
    ILogger<MediatrLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
            return await next(cancellationToken);

        var name = typeof(TRequest).Name;
        var start = Stopwatch.GetTimestamp();
        logger.LogDebug("→ {RequestName} handling", name);
        try
        {
            var response = await next(cancellationToken);
            logger.LogDebug("← {RequestName} handled in {ElapsedMs:0.0} ms",
                name, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "✖ {RequestName} threw after {ElapsedMs:0.0} ms",
                name, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            throw;
        }
    }
}
