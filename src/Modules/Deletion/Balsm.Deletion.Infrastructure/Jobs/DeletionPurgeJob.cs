using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace Balsm.Deletion.Infrastructure.Jobs;

/// <summary>
/// Nightly job that hard-purges accounts past their deletion grace period. FR-032.
/// Releases username reservations; retains deletion_log for 2 years (purge_at).
/// </summary>
public sealed class DeletionPurgeJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeletionPurgeJob> _logger;
    private const string DefaultCron = "0 4 * * *"; // 04:00 UTC nightly

    public DeletionPurgeJob(IServiceScopeFactory scopeFactory, ILogger<DeletionPurgeJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var schedule = CrontabSchedule.Parse(DefaultCron);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var next = schedule.GetNextOccurrence(now);
            var delay = next - now;

            _logger.LogInformation("Next deletion purge run at {Next:u} (in {Delay:hh\\:mm\\:ss})", next, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during deletion purge job");
            }
        }
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var accountDb = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var deletionDb = scope.ServiceProvider.GetRequiredService<DeletionDbContext>();

        var now = DateTime.UtcNow;

        // Accounts past grace period that are still in DELETION_REQUESTED
        var expired = await accountDb.UserAccounts
            .Where(a => a.DeletionState == DeletionState.DELETION_REQUESTED
                        && a.DeletionGraceUntil != null
                        && a.DeletionGraceUntil < now)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            _logger.LogInformation("Deletion purge: no accounts past grace period");
            return;
        }

        _logger.LogInformation("Deletion purge: purging {Count} accounts", expired.Count);

        foreach (var account in expired)
        {
            // Release username so it can be reclaimed
            var reservation = await accountDb.UsernameReservations
                .FirstOrDefaultAsync(r => r.UserId == account.Id && r.ReleasedAt == null, ct);
            if (reservation is not null)
                reservation.Release();

            // Hard-delete the account record (GDPR right-to-erasure)
            accountDb.UserAccounts.Remove(account);
        }

        await accountDb.SaveChangesAsync(ct);

        // Purge deletion_log entries past their retention window (2 years)
        var expiredLogs = await deletionDb.DeletionLogs
            .Where(l => l.PurgeAt < now)
            .ToListAsync(ct);

        if (expiredLogs.Count > 0)
        {
            deletionDb.DeletionLogs.RemoveRange(expiredLogs);
            await deletionDb.SaveChangesAsync(ct);
            _logger.LogInformation("Deletion purge: removed {Count} expired deletion log entries", expiredLogs.Count);
        }

        _logger.LogInformation("Deletion purge complete: purged {Count} accounts", expired.Count);
    }
}
