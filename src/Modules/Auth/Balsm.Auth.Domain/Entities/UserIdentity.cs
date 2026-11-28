namespace Balsm.Auth.Domain.Entities;

public sealed class UserIdentity
{
    public const string EmailProvider = "email";

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = string.Empty;

    /// The sign-in id the identity is keyed on: the provider `sub` for
    /// Google/Apple, the row's own id for email. Never the address.
    public string ProviderSubject { get; private set; } = string.Empty;

    /// The address as typed (trimmed, lowercased) — where mail is sent.
    public string? EmailNormalized { get; private set; }

    /// <see cref="ValueObjects.EmailCanonical"/> of the address — what lookups
    /// and the email-provider uniqueness match on.
    public string? EmailCanonical { get; private set; }

    public DateTime? EmailConfirmedAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// Argon2id-encoded password hash (salt + params embedded). Null until the
    /// user sets a password; only the email provider carries one.
    public string? PasswordHash { get; private set; }
    public DateTime? PasswordSetAt { get; private set; }

    /// Apple's refresh token for this identity, AES-GCM encrypted by the
    /// infrastructure layer. Kept only so account deletion can revoke it.
    public byte[]? AppleRefreshTokenCiphertext { get; private set; }

    /// When the user last signed in with Apple, i.e. last (re)granted consent.
    /// An Apple consent-revoked/account-delete event older than this is stale.
    public DateTime? AppleConsentAt { get; private set; }

    private UserIdentity() { }

    public static UserIdentity Create(Guid userId, string provider, string providerSubject, string? email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSubject);
        return new UserIdentity
        {
            UserId = userId,
            Provider = provider,
            ProviderSubject = providerSubject,
            EmailNormalized = Normalize(email),
            EmailCanonical = Canonical(email)
        };
    }

    /// An email identity, keyed on its own id so the address stays changeable.
    public static UserIdentity CreateEmail(Guid userId, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var identity = new UserIdentity
        {
            UserId = userId,
            Provider = EmailProvider,
            EmailNormalized = Normalize(email),
            EmailCanonical = Canonical(email)
        };
        identity.ProviderSubject = identity.Id.ToString();
        return identity;
    }

    /// Moves the identity to a new address. The sign-in id and the user stay;
    /// the confirmation goes, because the new address is unproven.
    public void ChangeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        EmailNormalized = Normalize(email);
        EmailCanonical = Canonical(email);
        EmailConfirmedAt = null;
    }

    /// Takes the address a social provider reports now. Null or blank means
    /// the token carried none, so the stored snapshot stays. A different
    /// mailbox drops the confirmation; re-confirming is the caller's call — it
    /// knows whether the provider vouched for the new address.
    public void RefreshProviderEmail(string? email)
    {
        if (Provider == EmailProvider)
            throw new InvalidOperationException("Email identities change address through ChangeEmail.");
        if (string.IsNullOrWhiteSpace(email)) return;
        var canonical = Canonical(email);
        if (canonical != EmailCanonical) EmailConfirmedAt = null;
        EmailNormalized = Normalize(email);
        EmailCanonical = canonical;
    }

    public void ConfirmEmail(DateTime at) => EmailConfirmedAt = at;

    /// Store a pre-computed Argon2id hash (hashing happens in the handler, which
    /// owns the PasswordHasher). Also stamps [PasswordSetAt].
    public void SetPasswordHash(string encodedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encodedHash);
        PasswordHash = encodedHash;
        PasswordSetAt = DateTime.UtcNow;
    }

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    /// Replaces any earlier Apple refresh token with a newer one.
    public void SetAppleRefreshToken(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        AppleRefreshTokenCiphertext = ciphertext;
    }

    public void ClearAppleRefreshToken() => AppleRefreshTokenCiphertext = null;

    public void MarkAppleConsent(DateTime at) => AppleConsentAt = at;

    private static string? Normalize(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static string? Canonical(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : ValueObjects.EmailCanonical.From(email);
}
