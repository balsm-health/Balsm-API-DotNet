using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Lifecycle;

public sealed class MigrationRecoveryService(
    IServiceScopeFactory scopeFactory,
    ILogger<MigrationRecoveryService> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Platform.PlatformDbContext>();

        // On a fresh database the schema (incl. MigrationStateRecords) does not exist
        // yet — migrations run after this recovery pass. Nothing to recover, and
        // querying the missing table would throw, so bail out early.
        var applied = await db.Database.GetAppliedMigrationsAsync(ct).ConfigureAwait(false);
        if (!applied.Any())
            return;

        var orphaned = await db.MigrationStateRecords
            .Where(r => r.CompletedAt == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (orphaned.Count == 0)
            return;

        logger.LogWarning(
            "Found {Count} incomplete migration state records — marking as failed",
            orphaned.Count);

        foreach (var record in orphaned)
        {
            record.ErrorMessage = "Recovered at startup — previous run did not complete";
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
