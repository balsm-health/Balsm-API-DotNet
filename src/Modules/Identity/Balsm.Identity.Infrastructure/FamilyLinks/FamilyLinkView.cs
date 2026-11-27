using Balsm.Identity.Domain.FamilyLinks;

namespace Balsm.Identity.Infrastructure.FamilyLinks;

/// <summary>The columns a <c>FamilyLinkDto</c> is built from — projected straight from the database for lists.</summary>
internal sealed record FamilyLinkView(
    Guid Id,
    Guid RequesterUserId,
    Guid RecipientUserId,
    string Relation,
    FamilyLinkStatus Status,
    byte[]? RequesterPublicKey,
    byte[]? RecipientPublicKey,
    long? RequesterCardVersion,
    long? RecipientCardVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? RespondedAt,
    DateTime? LinkedAt,
    DateTime? EndedAt)
{
    public static FamilyLinkView From(FamilyLink link) => new(
        link.Id,
        link.RequesterUserId,
        link.RecipientUserId,
        link.Relation,
        link.Status,
        link.RequesterPublicKey,
        link.RecipientPublicKey,
        link.CardOwnedBy(link.RequesterUserId)?.Version,
        link.CardOwnedBy(link.RecipientUserId)?.Version,
        link.CreatedAt,
        link.UpdatedAt,
        link.RespondedAt,
        link.LinkedAt,
        link.EndedAt);
}
