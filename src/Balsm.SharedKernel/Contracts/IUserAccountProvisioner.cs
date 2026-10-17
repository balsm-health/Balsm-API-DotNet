namespace Balsm.SharedKernel.Contracts;

/// <summary>
/// Published interface for creating the cross-plane user-account row when an
/// identity is first established. Implemented by the Account module and wired
/// in the composition root — Auth consumes this contract instead of touching
/// Account's DbContext (module boundary rule: no cross-module project
/// references, no shared tables).
/// </summary>
public interface IUserAccountProvisioner
{
    /// <summary>Creates a user account and returns its id.</summary>
    Task<Guid> ProvisionAsync(string countryCode, string preferredLanguage, CancellationToken ct = default);

    /// <summary>
    /// Creates an account under <paramref name="preferredId"/> when that id is a
    /// UUIDv7 and unused; otherwise under a fresh id. Returns the id actually used.
    /// Never fails because the preferred id is taken — callers must not learn
    /// which ids exist.
    /// </summary>
    Task<Guid> ProvisionWithPreferredIdAsync(
        string countryCode, string preferredLanguage, Guid preferredId, CancellationToken ct = default);
}
