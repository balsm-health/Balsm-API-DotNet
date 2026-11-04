namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>
/// The relation label the requester gives the person they add — the same
/// fixed, locale-independent set as the app's <c>Relationship</c> wire codes.
/// </summary>
public static class FamilyRelation
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "spouse", "partner", "parent", "child", "sibling", "grandparent",
        "relative", "friend", "guardian", "caregiver", "neighbour", "other",
    };

    public static bool IsAllowed(string? relation) => relation is not null && Allowed.Contains(relation);
}
