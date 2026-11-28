namespace Balsm.SharedKernel.Contracts;

/// <summary>
/// Published interface Auth asks before adopting a client-proposed account id:
/// the purge erases the account row and its identities, so only the deletion
/// log still knows the id was used. Implemented by the Deletion module and
/// wired in the composition root.
/// </summary>
public interface IDeletedAccountRegistry
{
    /// <summary>True when the account was deleted (its deletion is on record).</summary>
    Task<bool> WasDeletedAsync(Guid userId, CancellationToken ct = default);
}
