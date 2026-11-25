namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>
/// The relation label the requester gives the person they add — the fixed,
/// locale-independent wire codes the app's family-link sheet sends.
/// </summary>
public static class FamilyRelation
{
    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "son", "daughter", "mother", "father", "spouse", "neighbour", "friend", "other",
    };

    public static bool IsAllowed(string? relation) => relation is not null && Allowed.Contains(relation);
}
