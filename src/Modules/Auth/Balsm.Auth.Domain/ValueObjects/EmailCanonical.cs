namespace Balsm.Auth.Domain.ValueObjects;

/// The form two spellings of one mailbox share, used to find and dedupe email
/// identities. Never an address to send to: mail goes to the address as typed.
public static class EmailCanonical
{
    private const string Gmail = "gmail.com";
    private const string GoogleMail = "googlemail.com";

    /// Trim + lowercase. For gmail.com / googlemail.com: drop dots and any "+suffix"
    /// in the local part, domain → gmail.com. Other domains: lowercase only.
    public static string From(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var lowered = email.Trim().ToLowerInvariant();

        // Split at the first '@' — the migration's SQL backfill splits the same way.
        var at = lowered.IndexOf('@');
        if (at < 0) return lowered;

        var domain = lowered[(at + 1)..];
        if (domain is not (Gmail or GoogleMail)) return lowered;

        var local = lowered[..at];
        var plus = local.IndexOf('+');
        if (plus >= 0) local = local[..plus];
        return $"{local.Replace(".", string.Empty, StringComparison.Ordinal)}@{Gmail}";
    }
}
