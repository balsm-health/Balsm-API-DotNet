namespace Balsm.Auth.Domain.Entities;

/// A pending one-time-passcode challenge. Created when a code is requested
/// (only the HMAC hash is stored — never the code) and redeemed once on
/// successful verification.
public sealed class OtpChallenge
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string EmailNormalized { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ConsumedAt { get; private set; }

    private OtpChallenge() { }

    public static OtpChallenge Create(string emailNormalized, string codeHash, DateTime expiresAt) =>
        new() { EmailNormalized = emailNormalized, CodeHash = codeHash, ExpiresAt = expiresAt };

    /// True while the challenge is unredeemed and not past [ExpiresAt].
    public bool IsRedeemable => ConsumedAt is null && ExpiresAt > DateTime.UtcNow;

    /// Marks the challenge redeemed so it cannot be reused.
    public void Consume() => ConsumedAt = DateTime.UtcNow;
}
