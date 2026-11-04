using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.Data;
using Balsm.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Identity.Infrastructure.FamilyLinks;

/// <summary>
/// Identity-side implementation of the account-erasure hook: the deleted
/// account withdraws from every open link the way its owner could have —
/// cancelling its own pending requests, declining the ones addressed to it,
/// unlinking the rest — so the sealed cards on those links are purged.
/// </summary>
public sealed class FamilyLinkAccountEraser(IdentityDbContext db, TimeProvider clock) : IAccountFamilyLinkEraser
{
    /// <summary>Correlation id stamped on the audit rows this purge writes.</summary>
    public const string CorrelationId = "account-deletion";

    public async Task<int> EraseForAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var links = await db.FamilyLinks
            .Include(l => l.Consents)
            .Include(l => l.CardShares)
            .AsSplitQuery()
            .Where(l => (l.RequesterUserId == userId || l.RecipientUserId == userId)
                        && (l.Status == FamilyLinkStatus.Pending || l.Status == FamilyLinkStatus.Linked))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var link in links)
        {
            var from = link.Status;
            var declining = from == FamilyLinkStatus.Pending && link.RecipientUserId == userId;
            if (declining) link.Decline(userId, now);
            else link.End(userId, now);

            var action = declining ? FamilyLinkAuditAction.Declined
                : from == FamilyLinkStatus.Pending ? FamilyLinkAuditAction.Cancelled
                : FamilyLinkAuditAction.Unlinked;
            db.FamilyLinkAuditEntries.Add(FamilyLinkAuditEntry.Record(link.Id, userId, action, from, link.Status, CorrelationId, now));
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return links.Count;
    }
}
