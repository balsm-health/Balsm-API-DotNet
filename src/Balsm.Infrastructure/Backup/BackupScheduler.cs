using Balsm.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace Balsm.Infrastructure.Backup;

public sealed class BackupScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupScheduler> _logger;

    public BackupScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<BackupScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            string cron;
            int retention;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var cronEntry = await db.ServerConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == "backup_cron", stoppingToken)
                    .ConfigureAwait(false);
                var retentionEntry = await db.ServerConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == "backup_retention", stoppingToken)
                    .ConfigureAwait(false);

                cron = cronEntry?.Value ?? "0 2 * * *";
                retention = int.TryParse(retentionEntry?.Value, out var r) ? r : 30;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read backup schedule config; retrying in 1 minute");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var schedule = CrontabSchedule.TryParse(cron);
            if (schedule is null)
            {
                _logger.LogWarning("Invalid backup_cron expression '{Cron}'; using default '0 2 * * *'", cron);
                schedule = CrontabSchedule.Parse("0 2 * * *");
            }

            var now = DateTime.UtcNow;
            var next = schedule.GetNextOccurrence(now);
            var delay = next - now;

            _logger.LogInformation(
                "Next scheduled backup at {Next:u} (in {Delay:hh\\:mm\\:ss})", next, delay);

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
                var result = await backupService.BackupNowAsync(BackupTrigger.Scheduled, stoppingToken)
                    .ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "Scheduled backup completed: {Filename}", result.Value!.Filename);
                    await PruneOldBackupsAsync(scope, retention, stoppingToken).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogError("Scheduled backup failed: {Error}", result.Error?.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during scheduled backup");
            }
        }
    }

    private static async Task PruneOldBackupsAsync(
        IServiceScope scope,
        int retention,
        CancellationToken ct)
    {
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var files = await db.BackupFiles
            .Where(b => b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (files.Count <= retention)
            return;

        // Keep the most recent `retention` files; soft-delete the rest
        var toDelete = files.Skip(retention).ToList();
        foreach (var file in toDelete)
        {
            file.IsDeleted = true;
            file.DeletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
