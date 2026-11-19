using Balsm.SharedKernel.Results;

namespace Balsm.Infrastructure.Maintenance;

/// <summary>Expected failures when changing the maintenance mode.</summary>
public static class MaintenanceErrors
{
    /// <summary>Code shared by every input-validation failure.</summary>
    public const string ValidationCode = "maintenance.validation";

    /// <summary>The mode is pinned by the <c>Maintenance</c> configuration section.</summary>
    public static readonly Error ConfigOverride = new(
        "maintenance.config_override",
        "Maintenance mode is set by server configuration (Maintenance:Mode) and cannot be changed here.");

    /// <summary>The requested mode is not one of off / readonly / block.</summary>
    public static readonly Error InvalidMode = new(
        ValidationCode, "mode must be one of: off, readonly, block.");

    /// <summary>The message is longer than <see cref="MaintenanceLimits.MaxMessageLength"/>.</summary>
    public static readonly Error MessageTooLong = new(
        ValidationCode, $"message must be at most {MaintenanceLimits.MaxMessageLength} characters.");

    /// <summary>The announced end time is not in the future.</summary>
    public static readonly Error EndsAtInPast = new(
        ValidationCode, "ends_at must be in the future.");
}
