using Balsm.CareDirectory.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCrontab;

namespace Balsm.CareDirectory.Infrastructure.MapPacks;

/// <summary>
/// Nightly cron wrapper around <see cref="MapPackExportRunner"/> — see that
/// class for what a run actually does. This class owns only the timing loop
/// and the two things a run needs that come from outside the database: R2
/// configuration and the governorate registry file.
/// </summary>
public sealed class MapPackExportJob(
    IServiceScopeFactory scopeFactory,
    IOptions<MapPackR2Options> options,
    IHostEnvironment environment,
    ILogger<MapPackExportJob> logger) : BackgroundService
{
    private const string RegistryPath = "data/map-packs/governorates.json";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = CrontabSchedule.Parse(options.Value.ExportCron);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var next = schedule.GetNextOccurrence(now);
            var delay = next - now;

            logger.LogInformation(
                "Next map-pack export run at {Next:u} (in {Delay:hh\\:mm\\:ss})", next, delay);

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error during map-pack export job");
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            // Self-hosted deployments that never set R2 credentials must keep
            // running — the offline catalogue just stays empty, same as
            // before the first-ever nightly run.
            logger.LogWarning("Map-pack export disabled: R2 is not configured");
            return;
        }

        var registry = LoadRegistry();
        if (registry.Count == 0)
        {
            logger.LogWarning("Map-pack export skipped: {Path} not found or empty", RegistryPath);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareDirectoryDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IMapPackObjectStore>();
        var runner = new MapPackExportRunner(db, store, registry, settings, logger);

        await runner.RunAsync(ct).ConfigureAwait(false);
        logger.LogInformation("Map-pack export run complete");
    }

    private IReadOnlyList<GovernorateRef> LoadRegistry()
    {
        // Same probe order as CareDirectoryImportService: a published
        // deployment runs from the build output directory, but `dotnet run`
        // in development uses the project directory as its content root.
        foreach (var root in new[] { AppContext.BaseDirectory, environment.ContentRootPath })
        {
            var candidate = Path.Combine(root, RegistryPath);
            if (File.Exists(candidate))
            {
                return GovernorateRegistry.Parse(File.ReadAllText(candidate));
            }
        }

        return [];
    }
}
