namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>
/// Lifecycle of a family link: <c>pending → linked | declined | cancelled</c>,
/// then <c>linked → unlinked</c>. Declined, cancelled and unlinked are terminal.
/// </summary>
public enum FamilyLinkStatus
{
    Pending,
    Linked,
    Declined,
    Cancelled,
    Unlinked,
}
