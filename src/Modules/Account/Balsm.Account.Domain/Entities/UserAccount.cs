using Balsm.SharedKernel.Domain;

namespace Balsm.Account.Domain.Entities;

public enum DeletionState { ACTIVE, DELETION_REQUESTED, DELETION_CANCELLED }

public sealed class UserAccount : AggregateRoot
{
    public string? Handle { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Bio { get; private set; }
    public byte[]? DateOfBirthCiphertext { get; private set; }
    public string CountryCode { get; private set; } = string.Empty;
    public string PreferredLanguage { get; private set; } = "en";
    public DeletionState DeletionState { get; private set; } = DeletionState.ACTIVE;
    public DateTime? DeletionConfirmedAt { get; private set; }
    public DateTime? DeletionGraceUntil { get; private set; }
    public new DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private UserAccount() { }

    public static UserAccount Create(string countryCode, string preferredLanguage) =>
        new() { CountryCode = countryCode, PreferredLanguage = preferredLanguage };

    public void ClaimHandle(string handle) { Handle = handle.ToLowerInvariant(); UpdatedAt = DateTime.UtcNow; }
    public void SetDob(byte[] ciphertext) { DateOfBirthCiphertext = ciphertext; UpdatedAt = DateTime.UtcNow; }
    public void UpdateProfile(string? displayName, string? bio) { DisplayName = displayName; Bio = bio; UpdatedAt = DateTime.UtcNow; }
    public void ChangeCountry(string countryCode) { CountryCode = countryCode; UpdatedAt = DateTime.UtcNow; }
    public void ChangeLanguage(string language) { PreferredLanguage = language; UpdatedAt = DateTime.UtcNow; }

    public void RequestDeletion(DateTime graceUntil)
    {
        DeletionState = DeletionState.DELETION_REQUESTED;
        DeletionConfirmedAt = DateTime.UtcNow;
        DeletionGraceUntil = graceUntil;
        UpdatedAt = DateTime.UtcNow;
    }

    public void CancelDeletion() { DeletionState = DeletionState.DELETION_CANCELLED; UpdatedAt = DateTime.UtcNow; }
}
