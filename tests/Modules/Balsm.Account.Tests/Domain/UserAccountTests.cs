using Balsm.Account.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Domain;

public sealed class UserAccountTests
{
    private static readonly Guid AccountId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly DateTime GraceUntil = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithCountryAndLanguage_SetsBoth()
    {
        var account = UserAccount.Create("SA", "ar");

        account.Should().BeEquivalentTo(new { CountryCode = "SA", PreferredLanguage = "ar" });
    }

    [Fact]
    public void Create_NewAccount_IsActive()
    {
        var account = UserAccount.Create("SA", "en");

        account.DeletionState.Should().Be(DeletionState.ACTIVE);
    }

    [Fact]
    public void Create_NewAccount_HasNoProfileFields()
    {
        var account = UserAccount.Create("SA", "en");

        account.Should().BeEquivalentTo(new
        {
            Handle = (string?)null,
            FirstName = (string?)null,
            LastName = (string?)null,
            DisplayName = (string?)null,
            DateOfBirthCiphertext = (byte[]?)null,
            NationalIdCiphertext = (byte[]?)null,
        });
    }

    [Fact]
    public void CreateWithId_WithPreferredId_UsesThatId()
    {
        var account = UserAccount.CreateWithId(AccountId, "SA", "en");

        account.Id.Should().Be(AccountId);
    }

    [Fact]
    public void CreateWithId_WithPreferredId_SetsCountryAndLanguage()
    {
        var account = UserAccount.CreateWithId(AccountId, "EG", "ar");

        account.Should().BeEquivalentTo(new { CountryCode = "EG", PreferredLanguage = "ar" });
    }

    [Fact]
    public void ClaimHandle_MixedCaseHandle_StoresLowercase()
    {
        var account = UserAccount.Create("SA", "en");

        account.ClaimHandle("Jane_Doe");

        account.Handle.Should().Be("jane_doe");
    }

    [Fact]
    public void SetDob_WithCiphertext_StoresCiphertext()
    {
        var account = UserAccount.Create("SA", "en");
        var ciphertext = new byte[] { 1, 2, 3 };

        account.SetDob(ciphertext);

        account.DateOfBirthCiphertext.Should().Equal(ciphertext);
    }

    [Fact]
    public void SetNationalId_WithCiphertext_StoresCiphertext()
    {
        var account = UserAccount.Create("SA", "en");
        var ciphertext = new byte[] { 4, 5, 6 };

        account.SetNationalId(ciphertext);

        account.NationalIdCiphertext.Should().Equal(ciphertext);
    }

    [Fact]
    public void SetNationalId_WithNull_ClearsCiphertext()
    {
        var account = UserAccount.Create("SA", "en");
        account.SetNationalId([4, 5, 6]);

        account.SetNationalId(null);

        account.NationalIdCiphertext.Should().BeNull();
    }

    [Fact]
    public void UpdateProfile_FirstAndLastName_ComposesDisplayName()
    {
        var account = UserAccount.Create("SA", "en");

        account.UpdateProfile(firstName: "Jane", lastName: "Doe");

        account.Should().BeEquivalentTo(new { FirstName = "Jane", LastName = "Doe", DisplayName = "Jane Doe" });
    }

    [Fact]
    public void UpdateProfile_OnlyFirstName_KeepsExistingLastNameInDisplayName()
    {
        var account = UserAccount.Create("SA", "en");
        account.UpdateProfile(firstName: "Jane", lastName: "Doe");

        account.UpdateProfile(firstName: "Janet");

        account.DisplayName.Should().Be("Janet Doe");
    }

    [Fact]
    public void UpdateProfile_OnlyLastName_TrimsDisplayName()
    {
        var account = UserAccount.Create("SA", "en");

        account.UpdateProfile(lastName: "Doe");

        account.DisplayName.Should().Be("Doe");
    }

    [Fact]
    public void UpdateProfile_NoNameFields_LeavesDisplayNameUnchanged()
    {
        var account = UserAccount.Create("SA", "en");
        account.UpdateProfile(firstName: "Jane", lastName: "Doe");

        account.UpdateProfile(bio: "Synthetic bio");

        account.DisplayName.Should().Be("Jane Doe");
    }

    [Fact]
    public void UpdateProfile_NonNameFields_SetsThem()
    {
        var account = UserAccount.Create("SA", "en");

        account.UpdateProfile(bio: "Synthetic bio", gender: "female", nationality: "SA", phone: "+10000000000");

        account.Should().BeEquivalentTo(new
        {
            Bio = "Synthetic bio",
            Gender = "female",
            Nationality = "SA",
            Phone = "+10000000000",
        });
    }

    [Fact]
    public void UpdateProfile_AllNull_LeavesFieldsUnchanged()
    {
        var account = UserAccount.Create("SA", "en");
        account.UpdateProfile("Jane", "Doe", "Synthetic bio", "female", "SA", "+10000000000");

        account.UpdateProfile();

        account.Should().BeEquivalentTo(new
        {
            FirstName = "Jane",
            LastName = "Doe",
            DisplayName = "Jane Doe",
            Bio = "Synthetic bio",
            Gender = "female",
            Nationality = "SA",
            Phone = "+10000000000",
        });
    }

    [Fact]
    public void UpdateProfile_EmptyStrings_ClearsFields()
    {
        var account = UserAccount.Create("SA", "en");
        account.UpdateProfile("Jane", "Doe", "Synthetic bio", "female", "SA", "+10000000000");

        account.UpdateProfile(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

        account.Should().BeEquivalentTo(new
        {
            FirstName = string.Empty,
            LastName = string.Empty,
            DisplayName = string.Empty,
            Bio = string.Empty,
            Gender = string.Empty,
            Nationality = string.Empty,
            Phone = string.Empty,
        });
    }

    [Fact]
    public void ChangeCountry_NewCountry_UpdatesCountryCode()
    {
        var account = UserAccount.Create("SA", "en");

        account.ChangeCountry("AE");

        account.CountryCode.Should().Be("AE");
    }

    [Fact]
    public void ChangeLanguage_NewLanguage_UpdatesPreferredLanguage()
    {
        var account = UserAccount.Create("SA", "en");

        account.ChangeLanguage("ar");

        account.PreferredLanguage.Should().Be("ar");
    }

    [Fact]
    public void RequestDeletion_WithGraceDate_MarksDeletionRequested()
    {
        var account = UserAccount.Create("SA", "en");

        account.RequestDeletion(GraceUntil);

        account.Should().BeEquivalentTo(new
        {
            DeletionState = DeletionState.DELETION_REQUESTED,
            DeletionGraceUntil = (DateTime?)GraceUntil,
        });
    }

    [Fact]
    public void RequestDeletion_WithGraceDate_RecordsConfirmation()
    {
        var account = UserAccount.Create("SA", "en");

        account.RequestDeletion(GraceUntil);

        account.DeletionConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public void CancelDeletion_AfterRequest_MarksDeletionCancelled()
    {
        var account = UserAccount.Create("SA", "en");
        account.RequestDeletion(GraceUntil);

        account.CancelDeletion();

        account.DeletionState.Should().Be(DeletionState.DELETION_CANCELLED);
    }
}
