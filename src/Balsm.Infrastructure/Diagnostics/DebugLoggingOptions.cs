namespace Balsm.Infrastructure.Diagnostics;

/// <summary>
/// Debug-only diagnostics switches (config section <c>Debug</c>). All default
/// off; intended for local development. Request/response bodies may carry PHI,
/// so body logging is never enabled in the Production environment regardless of
/// these values — see the gate in <c>Program.cs</c>.
/// </summary>
public sealed class DebugLoggingOptions
{
    public const string SectionName = "Debug";

    /// <summary>Enable the HTTP request/response tracing middleware.</summary>
    public bool LogRequests { get; init; }

    /// <summary>
    /// Also capture request/response bodies (scrubbed). Heavier and higher PHI
    /// exposure than the request line alone, so it is a separate opt-in.
    /// </summary>
    public bool LogBodies { get; init; }

    /// <summary>Max body bytes to capture per direction; larger bodies are truncated.</summary>
    public int MaxBodyBytes { get; init; } = 8 * 1024;
}
