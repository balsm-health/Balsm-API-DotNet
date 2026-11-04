using Balsm.Identity.Domain.FamilyLinks;

namespace Balsm.Identity.Application.FamilyLinks;

/// <summary>Wire spelling of <see cref="FamilyLinkStatus"/> — lowercase, locale-independent.</summary>
public static class FamilyLinkWire
{
    public const string Outgoing = "outgoing";
    public const string Incoming = "incoming";

    public static string ToWire(FamilyLinkStatus status) => status.ToString().ToLowerInvariant();

    /// <summary>Parses a lowercase wire status; anything else (including the enum's own casing) is rejected.</summary>
    public static bool TryParseStatus(string? value, out FamilyLinkStatus status)
    {
        foreach (var candidate in Enum.GetValues<FamilyLinkStatus>())
        {
            if (string.Equals(ToWire(candidate), value, StringComparison.Ordinal))
            {
                status = candidate;
                return true;
            }
        }

        status = default;
        return false;
    }
}
