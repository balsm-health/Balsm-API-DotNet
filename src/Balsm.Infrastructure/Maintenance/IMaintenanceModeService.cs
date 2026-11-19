using Balsm.SharedKernel.Results;

namespace Balsm.Infrastructure.Maintenance;

/// <summary>Reads and changes the platform maintenance mode.</summary>
public interface IMaintenanceModeService
{
    /// <summary>
    /// Current state. Served from a short in-memory snapshot so anonymous polling
    /// by every app install does not reach the database on each request.
    /// </summary>
    ValueTask<MaintenanceState> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates and stores <paramref name="change"/>, and writes an audit row
    /// naming <paramref name="actor"/>. Fails with <c>maintenance.validation</c>
    /// on bad input and <c>maintenance.config_override</c> when the mode is pinned
    /// by configuration.
    /// </summary>
    Task<Result<MaintenanceState>> SetAsync(MaintenanceChange change, string actor, CancellationToken ct = default);
}
