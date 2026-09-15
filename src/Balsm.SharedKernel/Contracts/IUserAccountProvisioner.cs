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
}
