namespace Balsm.Infrastructure.Maintenance;

/// <summary>
/// Wire values for the platform maintenance mode. Kept as lowercase string
/// constants (not an enum) so the JSON contract is exactly <c>off</c> /
/// <c>readonly</c> / <c>block</c> regardless of the serializer's enum settings.
/// </summary>
public static class MaintenanceModes
{
    /// <summary>Normal operation.</summary>
    public const string Off = "off";

    /// <summary>Clients stay readable; clients pause writes, sign-in and sync. The API keeps accepting writes.</summary>
    public const string Readonly = "readonly";

    /// <summary>Clients show a full-screen page; the API refuses non-essential writes with 503.</summary>
    public const string Block = "block";

    /// <summary>All accepted values, in severity order.</summary>
    public static readonly IReadOnlyList<string> All = [Off, Readonly, Block];

    /// <summary>Normalises <paramref name="value"/> to a known mode, or <c>null</c> when it is not one.</summary>
    public static string? Normalize(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        return v is not null && All.Contains(v) ? v : null;
    }
}

/// <summary>
/// The current maintenance state, as stored and as served to clients.
/// Contains no PHI: the message is free text written by a Balsm super admin.
/// </summary>
/// <param name="Mode">One of <see cref="MaintenanceModes"/>.</param>
/// <param name="Message">Optional admin message to patients; <c>null</c> means "use the app's default copy".</param>
/// <param name="EndsAt">Announced end time (UTC); <c>null</c> means "not announced".</param>
/// <param name="UpdatedAt">When the state last changed (UTC); <c>null</c> if it never has.</param>
public sealed record MaintenanceState(string Mode, string? Message, DateTimeOffset? EndsAt, DateTimeOffset? UpdatedAt)
{
    /// <summary>The state used when nothing has ever been stored.</summary>
    public static readonly MaintenanceState Default = new(MaintenanceModes.Off, null, null, null);
}

/// <summary>Input for changing the maintenance state.</summary>
/// <param name="Mode">Requested mode; must be one of <see cref="MaintenanceModes"/>.</param>
/// <param name="Message">Optional message, at most <see cref="MaintenanceLimits.MaxMessageLength"/> characters.</param>
/// <param name="EndsAt">Optional announced end time; must be in the future when given.</param>
public sealed record MaintenanceChange(string? Mode, string? Message, DateTimeOffset? EndsAt);

/// <summary>Validation limits for <see cref="MaintenanceChange"/>.</summary>
public static class MaintenanceLimits
{
    /// <summary>Longest admin message accepted (fits the app's sheet without truncation).</summary>
    public const int MaxMessageLength = 280;
}

/// <summary>Envelope for maintenance responses: <c>{ "data": { ... } }</c>.</summary>
/// <param name="Data">The current maintenance state.</param>
public sealed record MaintenanceStatusResponse(MaintenanceState Data);
