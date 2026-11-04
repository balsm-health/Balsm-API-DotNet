using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.Data;
using Balsm.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balsm.Identity.Infrastructure.FamilyLinks;

/// <summary>
/// Loading and saving shared by the family-link command handlers: the aggregate
/// with its consents and card shares, and one transaction for the change plus
/// its audit row.
/// </summary>
public sealed class FamilyLinkStore(IdentityDbContext db, TimeProvider clock, ILogger<FamilyLinkStore> logger)
{
    public DateTime UtcNow => clock.GetUtcNow().UtcDateTime;

    public Task<FamilyLink?> LoadAsync(Guid linkId, CancellationToken ct) =>
        db.FamilyLinks
            .Include(l => l.Consents)
            .Include(l => l.CardShares)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == linkId, ct);

    /// <summary>Adds the audit row and commits it with the aggregate's change in one SaveChanges.</summary>
    public async Task<Result> SaveAsync(
        FamilyLink link,
        Guid actorUserId,
        FamilyLinkAuditAction action,
        FamilyLinkStatus? fromStatus,
        string? correlationId,
        CancellationToken ct)
    {
        db.FamilyLinkAuditEntries.Add(FamilyLinkAuditEntry.Record(
            link.Id, actorUserId, action, fromStatus, link.Status, correlationId, UtcNow));
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Lost a race on the concurrency stamp or on the one-card-per-owner
            // index. Nothing was written; the caller retries against fresh state.
            logger.LogWarning(ex,
                "Family link change lost a race. {CorrelationId} {UserId} {Module} {Action} {FamilyLinkId}",
                correlationId, actorUserId, "Identity", action, link.Id);
            db.ChangeTracker.Clear();
            return Result.Failure(FamilyLinkErrors.ConcurrentUpdate);
        }

        logger.LogInformation(
            "Family link changed. {CorrelationId} {UserId} {Module} {Action} {FamilyLinkId} {FromStatus} {ToStatus}",
            correlationId, actorUserId, "Identity", action, link.Id, fromStatus, link.Status);
        return Result.Success();
    }
}
