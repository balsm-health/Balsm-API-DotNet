using Balsm.Infrastructure.Backup;
using Balsm.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace Balsm.Infrastructure.Audit;

public sealed class AuditRetentionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditRetentionJob> _logger;

    public AuditRetentionJob(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditRetentionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            string cron;
            int retentionYears;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var cronEntry = await db.ServerConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == "audit_retention_cron", stoppingToken)
                    .ConfigureAwait(false);
                var retentionEntry = await db.ServerConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Key == "audit_retention_years", stoppingToken)
                    .ConfigureAwait(false);

                cron = cronEntry?.Value ?? "0 3 * * *";
                retentionYears = int.TryParse(retentionEntry?.Value, out var y) ? y : 2;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read audit retention config; retrying in 1 minute");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var schedule = CrontabSchedule.TryParse(cron);
            if (schedule is null)
            {
                _logger.LogWarning("Invalid audit_retention_cron '{Cron}'; using default '0 3 * * *'", cron);
                schedule = CrontabSchedule.Parse("0 3 * * *");
            }

            var now = DateTime.UtcNow;
            var next = schedule.GetNextOccurrence(now);
            var delay = next - now;

            _logger.LogInformation(
                "Next audit retention run at {Next:u} (in {Delay:hh\\:mm\\:ss})", next, delay);

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
                await RunRetentionAsync(retentionYears, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during audit retention job");
            }
        }
    }

    private async Task RunRetentionAsync(int retentionYears, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sink = scope.ServiceProvider.GetRequiredService<AuditExportSink>();

        var cutoff = DateTime.UtcNow.AddYears(-retentionYears);

        var oldLogs = await db.AuditLogs
            .Where(l => l.OccurredAt < cutoff && !l.IsDeleted)
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (oldLogs.Count == 0)
        {
            _logger.LogInformation("Audit retention: no logs older than {Cutoff:u} to archive", cutoff);
            return;
        }

        _logger.LogInformation(
            "Archiving {Count} audit log entries older than {Cutoff:u}", oldLogs.Count, cutoff);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var archivePath = System.IO.Path.Combine("backups", $"audit-{timestamp}.jsonl");

        var archive = await sink.WriteAsync(oldLogs, archivePath, ct).ConfigureAwait(false);

        // Soft-delete archived rows
        foreach (var log in oldLogs)
        {
            log.IsDeleted = true;
            log.DeletedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Audit retention complete: archived {Count} rows to {Filename} (SHA256: {Sha256})",
            archive.RowCount, archive.Filename, archive.Sha256);
    }
}
