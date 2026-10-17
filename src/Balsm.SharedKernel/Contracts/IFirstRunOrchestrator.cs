namespace Balsm.SharedKernel.Contracts;

/// <summary>
/// Orchestrates first-run initialisation (workspace seeding, etc.).
/// Implemented in Balsm.API composition root; injected into Balsm.Supervisor.
/// </summary>
public interface IFirstRunOrchestrator
{
    /// <summary>Creates the singleton workspace on first-time setup.</summary>
    Task SeedWorkspaceAsync(string name, string slug, string locale, CancellationToken ct = default);
}
