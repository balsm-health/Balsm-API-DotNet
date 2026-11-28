using Balsm.Account.Domain.Entities;
using Balsm.CareTeam.Infrastructure.Data;
using Balsm.Account.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Data;
using Balsm.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
using System.Security.Cryptography;
using System.Text;

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

    /// <summary>
    /// How long past the grace period a failed Apple revoke keeps the account
    /// for a nightly retry before the purge gives up and erases it anyway.
    /// </summary>
    internal static readonly TimeSpan AppleRevokeRetryWindow = TimeSpan.FromDays(5);

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

    internal async Task PurgeAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var accountDb = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var deletionDb = scope.ServiceProvider.GetRequiredService<DeletionDbContext>();
        var careTeamDb = scope.ServiceProvider.GetRequiredService<CareTeamDbContext>();

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

        var kept = 0;
        foreach (var account in expired)
        {
            // Release username so it can be reclaimed
            var reservation = await accountDb.UsernameReservations
                .FirstOrDefaultAsync(r => r.UserId == account.Id && r.ReleasedAt == null, ct);
            if (reservation is not null)
                reservation.Release();

            // Cloud PHI mirror: care-team rows and their audit trail (FR-512).
            await PurgeCareTeamAsync(careTeamDb, account.Id, ct);

            // Auth data: revoke the Apple grant (and record the outcome on the
            // deletion log) before erasing the identities that hold its token.
            // Resolved inside the guard so an Auth wiring failure skips only
            // these steps, never the erasure above.
            // If the Apple revoke failed inside the retry window, or the auth
            // data could not be erased, the account is kept, still
            // DELETION_REQUESTED, so tomorrow's run retries it; the other
            // erasers are idempotent.
            var giveUpOnRevoke = account.DeletionGraceUntil!.Value + AppleRevokeRetryWindow < now;
            if (!await EraseAuthAsync(scope.ServiceProvider, deletionDb, account.Id, giveUpOnRevoke, ct))
            {
                _logger.LogWarning("Deletion purge: auth data not erased for user {UserId}; account kept for retry", account.Id);
                kept++;
                continue;
            }

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

        _logger.LogInformation("Deletion purge complete: purged {Count} accounts, kept {Kept} for retry", expired.Count - kept, kept);
    }

    /// <summary>
    /// Returns true when the account's auth data is gone. A failed Apple revoke
    /// is recorded as failed_retrying and keeps the auth data (the identity
    /// holds the only copy of the token) unless <paramref name="giveUpOnRevoke"/>,
    /// when it is recorded as failed_final and the auth data is erased.
    /// </summary>
    private async Task<bool> EraseAuthAsync(
        IServiceProvider services, DeletionDbContext deletionDb, Guid userId, bool giveUpOnRevoke, CancellationToken ct)
    {
        var retryRevoke = false;
        try
        {
            var revoker = services.GetRequiredService<IAppleGrantRevoker>();
            string status;
            try
            {
                status = await revoker.RevokeForAccountAsync(userId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Apple revoke threw for user {UserId}", userId);
                status = "failed";
            }

            var logStatus = ToLogStatus(status, giveUpOnRevoke);
            retryRevoke = logStatus == "failed_retrying";

            // Persist the outcome now: the Apple call cannot be undone, so the
            // record of it must not depend on a later save succeeding.
            var hash = ComputeHash(userId);
            var log = await deletionDb.DeletionLogs.FirstOrDefaultAsync(l => l.UserIdHash == hash, ct);
            if (log is null)
            {
                _logger.LogWarning("Deletion purge: no deletion log for user {UserId}; Apple revoke status {Status} not recorded",
                    userId, status);
            }
            else
            {
                // A retry sees no identity or token (already erased or revoked):
                // keep the status an earlier run recorded instead of overwriting
                // it. failed_retrying still moves on to succeeded or failed_final.
                if (!(logStatus == "not_applicable" && log.AppleRevokeStatus is not null))
                {
                    log.SetAppleRevokeStatus(logStatus);
                    await deletionDb.SaveChangesAsync(ct);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Apple revoke step failed for user {UserId}", userId);
        }

        if (retryRevoke) return false;

        try
        {
            var eraser = services.GetRequiredService<IAccountAuthEraser>();
            await eraser.EraseForAccountAsync(userId, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Auth erasure failed for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Maps the revoker's outcome to the vocabulary deletion_log.apple_revoke_status
    /// accepts (CK_deletion_log_apple_revoke_status). A failure (including
    /// missing Apple config) is retried nightly until the retry window ends,
    /// then recorded as final.
    /// </summary>
    internal static string ToLogStatus(string outcome, bool giveUpOnRevoke) => outcome switch
    {
        "revoked" => "succeeded",
        "no_apple_identity" or "no_refresh_token" => "not_applicable",
        _ => giveUpOnRevoke ? "failed_final" : "failed_retrying",
    };

    /// <summary>The user-id hash the intake handler stamps on the deletion log.</summary>
    internal static string ComputeHash(Guid userId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId.ToString())));

    /// <summary>
    /// FR-512: hard-delete every care-team row for a purged account, tombstones
    /// included — IgnoreQueryFilters, or soft-deleted rows would survive the purge
    /// that is supposed to erase them.
    /// </summary>
    internal static async Task PurgeCareTeamAsync(
        CareTeamDbContext careTeamDb, Guid userId, CancellationToken ct)
    {
        await careTeamDb.CareProviders
            .IgnoreQueryFilters()
            .Where(p => p.UserId == userId)
            .ExecuteDeleteAsync(ct);

        await careTeamDb.CareTeamAuditLogs
            .Where(a => a.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }
}
