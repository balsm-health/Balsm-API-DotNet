namespace Balsm.SharedKernel.Contracts;

/// <summary>
/// Published interface the account-deletion purge calls so an erased account
/// leaves no open family link and no sealed emergency card behind. Implemented
/// by the Identity module and wired in the composition root.
/// </summary>
public interface IAccountFamilyLinkEraser
{
    /// <summary>
    /// Ends every open link the account is a party to (cancel / decline /
    /// unlink) and purges every sealed card on those links, in either
    /// direction. The audit rows stay. Returns how many links were ended.
    /// </summary>
    Task<int> EraseForAccountAsync(Guid userId, CancellationToken ct = default);
}
