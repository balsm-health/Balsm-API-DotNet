namespace Balsm.Infrastructure.Lifecycle;

public sealed class StartupOptions
{
    public const string SectionName = "Startup";

    /// <summary>
    /// Cold-start budget. If the host takes longer than this from process start to
    /// "application started", a warning is logged. Phase 0 exit criterion targets
    /// &lt; 10 seconds on minimum-spec hardware.
    /// </summary>
    public int WarnThresholdSeconds { get; set; } = 10;
}
