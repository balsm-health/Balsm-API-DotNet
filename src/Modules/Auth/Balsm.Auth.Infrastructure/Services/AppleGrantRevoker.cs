using Balsm.Auth.Infrastructure.Data;
using Balsm.Infrastructure.Auth;
using Balsm.Infrastructure.Encryption;
using Balsm.SharedKernel.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balsm.Auth.Infrastructure.Services;

/// <summary>
/// Auth-side implementation of the Apple grant revocation hook. Decrypts the
/// stored refresh token, asks Apple to revoke it and forgets it on success.
/// Never throws: a failure for one account must not stop the purge of others.
/// Logs the user id only, never the token or an address.
/// </summary>
public sealed class AppleGrantRevoker(
    AuthDbContext db,
    AppleTokenClient client,
    AppleRefreshTokenCipher cipher,
    ILogger<AppleGrantRevoker> logger) : IAppleGrantRevoker
{
    private const string AppleProvider = "apple";

    public async Task<string> RevokeForAccountAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var identity = await db.UserIdentities
                .FirstOrDefaultAsync(i => i.UserId == userId && i.Provider == AppleProvider, ct)
                .ConfigureAwait(false);
            if (identity is null) return "no_apple_identity";
            if (identity.AppleRefreshTokenCiphertext is not { Length: > 0 } blob) return "no_refresh_token";
            if (!client.IsConfigured)
            {
                logger.LogWarning("Apple revoke config missing, skipping for user {UserId}", userId);
                return "config_missing";
            }

            var outcome = await client.RevokeAsync(cipher.Decrypt(blob), ct).ConfigureAwait(false);
            switch (outcome)
            {
                case AppleRevokeOutcome.Revoked:
                    identity.ClearAppleRefreshToken();
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return "revoked";
                case AppleRevokeOutcome.ConfigMissing:
                    return "config_missing";
                default:
                    return "failed";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Apple revoke failed for user {UserId}", userId);
            return "failed";
        }
    }
}
