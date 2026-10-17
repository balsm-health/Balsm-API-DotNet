namespace Balsm.Auth.Domain.Entities;

/// A pending one-time-passcode challenge. Created when a code is requested
/// (only the HMAC hash is stored — never the code) and redeemed once on
/// successful verification.
public sealed class OtpChallenge
{
    /// Tries one code allows, the right one included. A six-digit code lives
    /// ten minutes; without a cap it can simply be guessed.
    public const int MaxAttempts = 5;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string EmailNormalized { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;

    /// HMAC hash of the magic sign-in link token (raw token is emailed, only the
    /// hash is stored). Null for challenges created before link tokens existed.
    public string? LinkTokenHash { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ConsumedAt { get; private set; }

    /// Code comparisons made against this challenge. Raised in the database
    /// before each comparison (see CredentialAttempts), so parallel guesses
    /// cannot share a slot.
    public int Attempts { get; private set; }

    private OtpChallenge() { }

    public static OtpChallenge Create(
        string emailNormalized, string codeHash, DateTime expiresAt, string? linkTokenHash = null) =>
        new()
        {
            EmailNormalized = emailNormalized,
            CodeHash = codeHash,
            ExpiresAt = expiresAt,
            LinkTokenHash = linkTokenHash,
        };

    /// True while the challenge is unredeemed, not past [ExpiresAt], and has
    /// tries left.
    public bool IsRedeemable => ConsumedAt is null && ExpiresAt > DateTime.UtcNow && Attempts < MaxAttempts;

    /// Marks the challenge redeemed so it cannot be reused.
    public void Consume() => ConsumedAt = DateTime.UtcNow;
}
