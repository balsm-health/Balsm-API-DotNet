using System.Linq.Expressions;
using Balsm.Identity.Domain.FamilyLinks;

namespace Balsm.Identity.Infrastructure.FamilyLinks;

/// <summary>Database-side projection of a link to <see cref="FamilyLinkView"/> — card versions as correlated subqueries.</summary>
internal static class FamilyLinkProjection
{
    public static readonly Expression<Func<FamilyLink, FamilyLinkView>> ToView = l => new FamilyLinkView(
        l.Id,
        l.RequesterUserId,
        l.RecipientUserId,
        l.Relation,
        l.Status,
        l.RequesterPublicKey,
        l.RecipientPublicKey,
        l.CardShares.Where(c => c.OwnerUserId == l.RequesterUserId).Select(c => (long?)c.Version).FirstOrDefault(),
        l.CardShares.Where(c => c.OwnerUserId == l.RecipientUserId).Select(c => (long?)c.Version).FirstOrDefault(),
        l.CreatedAt,
        l.UpdatedAt,
        l.RespondedAt,
        l.LinkedAt,
        l.EndedAt);
}
