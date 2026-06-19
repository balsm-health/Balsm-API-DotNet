namespace Balsm.Deletion.Domain.Entities;

public sealed class DeletionLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string UserIdHash { get; private set; } = string.Empty;
    public string CountryCodeAtDeletion { get; private set; } = string.Empty;
    public string? ReasonCode { get; private set; }
    public string? AppleRevokeStatus { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime PurgeAt { get; private set; }

    private DeletionLog() { }

    public static DeletionLog Create(string userIdHash, string countryCode, string? reasonCode = null) =>
        new()
        {
            UserIdHash = userIdHash,
            CountryCodeAtDeletion = countryCode,
            ReasonCode = reasonCode,
            PurgeAt = DateTime.UtcNow.AddYears(2)
        };

    public void SetAppleRevokeStatus(string status) => AppleRevokeStatus = status;
}
