namespace Balsm.Auth.Domain.Entities;

public sealed class UserIdentity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string ProviderSubject { get; private set; } = string.Empty;
    public string? EmailNormalized { get; private set; }
    public DateTime? EmailConfirmedAt { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// Argon2id-encoded password hash (salt + params embedded). Null until the
    /// user sets a password; only the email provider carries one.
    public string? PasswordHash { get; private set; }
    public DateTime? PasswordSetAt { get; private set; }

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
            EmailNormalized = email?.ToLowerInvariant()
        };
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
}
