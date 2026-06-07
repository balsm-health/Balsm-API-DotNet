using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Infrastructure.Lifecycle;

/// <summary>
/// Records and reports cold-start time (process start → application started) so the
/// Phase 0 "&lt; 10 s startup" exit criterion is observable in production logs rather
/// than only at manual test time. Logs Information always; Warning when the measured
/// startup exceeds <see cref="StartupOptions.WarnThresholdSeconds"/>.
/// </summary>
public sealed class StartupTimingObserver(
    IHostApplicationLifetime lifetime,
    IOptions<StartupOptions> options,
    ILogger<StartupTimingObserver> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(ReportStartupTime);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ReportStartupTime()
    {
        var elapsed = MeasureElapsedSinceProcessStart();
        if (elapsed is null)
        {
            logger.LogInformation("Server ready (startup duration unavailable on this platform)");
            return;
        }

        var seconds = elapsed.Value.TotalSeconds;
        var threshold = options.Value.WarnThresholdSeconds;

        if (seconds > threshold)
        {
            logger.LogWarning(
                "Cold start took {StartupSeconds:F1}s — exceeds {ThresholdSeconds}s budget",
                seconds, threshold);
        }
        else
        {
            logger.LogInformation(
                "Cold start completed in {StartupSeconds:F1}s (budget {ThresholdSeconds}s)",
                seconds, threshold);
        }
    }

    private static TimeSpan? MeasureElapsedSinceProcessStart()
    {
        try
        {
            var startUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var elapsed = DateTime.UtcNow - startUtc;
            // Guard against clock skew / sandboxed environments returning bogus values.
            return elapsed < TimeSpan.Zero ? null : elapsed;
        }
        catch (Exception)
        {
            // Process.StartTime can throw under restricted permissions; timing is
            // best-effort and must never block or fail startup.
            return null;
        }
    }
}
