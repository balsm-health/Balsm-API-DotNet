namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>What a <see cref="FamilyLinkAuditEntry"/> records.</summary>
public enum FamilyLinkAuditAction
{
    Requested,
    Approved,
    Declined,
    Cancelled,
    Unlinked,
    KeyUpdated,
    CardShared,
    CardRead,
    CardRevoked,
}
