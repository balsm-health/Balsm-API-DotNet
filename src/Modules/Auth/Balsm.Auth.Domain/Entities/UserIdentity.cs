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
}
