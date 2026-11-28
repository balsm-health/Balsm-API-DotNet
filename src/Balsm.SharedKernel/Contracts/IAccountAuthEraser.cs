namespace Balsm.SharedKernel.Contracts;

/// <summary>
/// Published interface the account-deletion purge calls so an erased account
/// leaves no sign-in identity and no refresh token behind. Implemented by the
/// Auth module and wired in the composition root.
/// </summary>
public interface IAccountAuthEraser
{
    /// <summary>
    /// Hard-deletes the account's identities (including any stored Apple
    /// refresh token) and refresh tokens. Returns how many rows were deleted.
    /// </summary>
    Task<int> EraseForAccountAsync(Guid userId, CancellationToken ct = default);
}
