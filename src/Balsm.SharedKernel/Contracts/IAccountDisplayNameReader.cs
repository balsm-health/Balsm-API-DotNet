namespace Balsm.SharedKernel.Contracts;

/// <summary>
/// Published interface for the non-PHI names another module may show for an
/// account: its @handle and display name. Implemented by the Account module and
/// wired in the composition root.
/// </summary>
public interface IAccountDisplayNameReader
{
    /// <summary>
    /// Names for each id that has an account. Ids with no account, or one being deleted,
    /// are absent from the result rather than mapped to an empty name.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, AccountDisplayName>> GetAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}

/// <summary>An account's public-facing names. Either may be null when the account never set it.</summary>
public sealed record AccountDisplayName(string? Handle, string? DisplayName);
