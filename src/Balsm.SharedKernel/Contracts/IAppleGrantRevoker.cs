namespace Balsm.SharedKernel.Contracts;

/// <summary>
/// Published interface the account-deletion purge calls to revoke the user's
/// Sign in with Apple grant (App Store guideline 5.1.1(v)). Implemented by the
/// Auth module and wired in the composition root.
/// </summary>
public interface IAppleGrantRevoker
{
    /// <summary>
    /// Revokes the account's Apple grant with the stored refresh token and, on
    /// success, clears that token. Never throws. Returns one of:
    /// "no_apple_identity", "no_refresh_token", "config_missing", "revoked", "failed".
    /// </summary>
    Task<string> RevokeForAccountAsync(Guid userId, CancellationToken ct = default);
}
