using Balsm.SharedKernel.Domain;

namespace Balsm.Account.Domain.Entities;

public enum DeletionState { ACTIVE, DELETION_REQUESTED, DELETION_CANCELLED }

public sealed class UserAccount : AggregateRoot
{
    public string? Handle { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Bio { get; private set; }
    public string? Gender { get; private set; }
    public string? Nationality { get; private set; }
    public string? Phone { get; private set; }
    public byte[]? DateOfBirthCiphertext { get; private set; }
    // National ID is sensitive PII/PHI — always field-level encrypted, never
    // stored or logged in plaintext (same treatment as date of birth).
    public byte[]? NationalIdCiphertext { get; private set; }
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
    public void SetNationalId(byte[]? ciphertext) { NationalIdCiphertext = ciphertext; UpdatedAt = DateTime.UtcNow; }

    /// <summary>Updates the non-encrypted profile fields. Null leaves a field
    /// unchanged; use the dedicated setters for the encrypted DOB / national ID.</summary>
    public void UpdateProfile(
        string? firstName = null,
        string? lastName = null,
        string? bio = null,
        string? gender = null,
        string? nationality = null,
        string? phone = null)
    {
        if (firstName is not null) FirstName = firstName;
        if (lastName is not null) LastName = lastName;
        if (firstName is not null || lastName is not null)
            DisplayName = $"{FirstName} {LastName}".Trim();
        if (bio is not null) Bio = bio;
        if (gender is not null) Gender = gender;
        if (nationality is not null) Nationality = nationality;
        if (phone is not null) Phone = phone;
        UpdatedAt = DateTime.UtcNow;
    }
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
