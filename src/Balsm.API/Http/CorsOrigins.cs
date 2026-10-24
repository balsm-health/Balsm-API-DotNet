namespace Balsm.API.Http;

/// <summary>
/// Which browser origins may call the API cross-origin.
/// </summary>
/// <remarks>
/// Two kinds: the local admin panel (<c>localhost</c>, <c>127.0.0.1</c>,
/// <c>*.local</c>), and the origins listed in <c>Cors:AllowedOrigins</c> — the
/// patient web app, which is served from another host than the API. A listed
/// origin must match exactly (scheme, host and port); there are no wildcards.
/// </remarks>
public static class CorsOrigins
{
    /// <summary>Configuration key holding the comma-separated origin list.</summary>
    public const string ConfigKey = "Cors:AllowedOrigins";

    /// <summary>
    /// Splits the configured value into origins. Entries that are not an
    /// absolute http(s) origin are dropped, and a trailing slash is ignored.
    /// </summary>
    public static IReadOnlySet<string> Parse(string? configured)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in (configured ?? string.Empty).Split(
                     [',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Uri.TryCreate(entry, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                && uri.AbsolutePath == "/" && uri.Query.Length == 0)
            {
                origins.Add(uri.GetLeftPart(UriPartial.Authority));
            }
        }

        return origins;
    }

    public static bool IsAllowed(string origin, IReadOnlySet<string> configured)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host is "localhost" or "127.0.0.1"
               || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
               || configured.Contains(origin);
    }
}
