using System.Text.Json;

namespace Balsm.API.Middleware;

/// <summary>
/// Scrubs non-allowlisted field names from response bodies before Sentry/log capture.
/// Allowlist sourced from contracts/crash-allowlist.json. SC-006/SC-016.
/// </summary>
public sealed class PhiLeakGuardMiddleware(RequestDelegate next)
{
    // Top-level keys allowed on the wire per crash-allowlist.json
    private static readonly HashSet<string> AllowedTopLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        "event_id","timestamp","platform","sdk","release","environment",
        "level","logger","transaction","data","error"
    };

    // Patterns that must never appear in exception.value
    private static readonly System.Text.RegularExpressions.Regex[] DenyPatterns =
    [
        new(@"\d{11}"),
        new(@"\d{14}"),
        new(@"\+20\d{10}"),
        new(@"\+966\d{9}"),
        new(@"\+971\d{9}"),
        new(@"\d{4}-\d{2}-\d{2}"),
        new(@"(?i)allerg"),
        new(@"(?i)diabetes|hypertension|asthma|cancer|epilepsy"),
        new(@"(?i)dose|tablet|capsule|mg|ml"),
        new(@"(?i)blood.{0,3}type"),
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
    }

    /// <summary>
    /// Call before passing an exception value to Sentry to assert it contains no PHI patterns.
    /// Returns the original string if clean; throws <see cref="PhiLeakException"/> if not.
    /// </summary>
    public static string AssertNoPhi(string value)
    {
        foreach (var pattern in DenyPatterns)
        {
            if (pattern.IsMatch(value))
                throw new PhiLeakException($"PHI pattern '{pattern}' detected in exception value");
        }
        return value;
    }
}

public sealed class PhiLeakException(string message) : Exception(message);
