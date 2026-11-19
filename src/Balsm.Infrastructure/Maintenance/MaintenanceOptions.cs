namespace Balsm.Infrastructure.Maintenance;

/// <summary>
/// Optional configuration override for the maintenance state (section <c>Maintenance</c>).
///
/// The admin portal — the only super-admin surface — does not exist on a cloud
/// run (<c>AdminPortal:Enabled=false</c>). On cloud, an operator with access to
/// the deployment's variables sets <c>Maintenance__Mode</c> (plus optional
/// <c>Maintenance__Message</c> / <c>Maintenance__EndsAt</c>); when set, it wins
/// over the stored state and the admin write is refused with a conflict.
/// </summary>
public sealed class MaintenanceOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Maintenance";

    /// <summary>Override mode (<c>off</c> / <c>readonly</c> / <c>block</c>); empty means "use the stored state".</summary>
    public string? Mode { get; set; }

    /// <summary>Override message shown to patients.</summary>
    public string? Message { get; set; }

    /// <summary>Override announced end time (ISO-8601).</summary>
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>How long a read is served from memory before the store is consulted again.</summary>
    public int CacheSeconds { get; set; } = 10;
}
