using Balsm.Account.Application.Commands;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Handlers;
using Balsm.Infrastructure.Encryption;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Handlers;

public sealed class UpdateProfileHandlerTests : IDisposable
{
    private static readonly DateOnly AdultDob = new(1990, 1, 1);
    // Under 18 until 2038 — fixed so the test does not depend on today's date.
    private static readonly DateOnly MinorDob = new(2020, 1, 1);
    private const string SyntheticNationalId = "0000000001";

    private readonly AccountTestDatabase _database = new();
    private readonly DobEncryptionService _encryption = AccountTestDatabase.CreateEncryption();

    [Fact]
    public async Task Handle_PlainFields_PersistsThem()
    {
        await _database.SeedAccountAsync();
        var handler = CreateHandler();

        await handler.Handle(
            Command(firstName: "Jane", lastName: "Doe", bio: "Synthetic bio", gender: "female", nationality: "SA", phone: "+10000000000"),
            CancellationToken.None);

        var account = await LoadAsync();
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
    public async Task Handle_AdultDob_PersistsEncryptedDob()
    {
        await _database.SeedAccountAsync();
        var handler = CreateHandler();

        await handler.Handle(Command(dateOfBirth: AdultDob), CancellationToken.None);

        var account = await LoadAsync();
        _encryption.Decrypt(account.DateOfBirthCiphertext!).Should().Be(AdultDob);
    }

    [Fact]
    public async Task Handle_MinorDob_ThrowsUnderEighteen()
    {
        await _database.SeedAccountAsync();
        var handler = CreateHandler();

        var act = () => handler.Handle(Command(dateOfBirth: MinorDob), CancellationToken.None);

        await act.Should().ThrowAsync<UnderEighteenException>();
    }

    [Fact]
    public async Task Handle_MinorDob_DoesNotPersistAnyChange()
    {
        await _database.SeedAccountAsync();
        var handler = CreateHandler();

        var act = () => handler.Handle(Command(firstName: "Jane", dateOfBirth: MinorDob), CancellationToken.None);

        await act.Should().ThrowAsync<UnderEighteenException>();
        var account = await LoadAsync();
        account.Should().BeEquivalentTo(new { FirstName = (string?)null, DateOfBirthCiphertext = (byte[]?)null });
    }

    [Fact]
    public async Task Handle_NullDob_LeavesExistingDobUnchanged()
    {
        var existing = _encryption.Encrypt(AdultDob);
        await _database.SeedAccountAsync(a => a.SetDob(existing));
        var handler = CreateHandler();

        await handler.Handle(Command(firstName: "Jane"), CancellationToken.None);

        var account = await LoadAsync();
        account.DateOfBirthCiphertext.Should().Equal(existing);
    }

    [Fact]
    public async Task Handle_NationalId_PersistsEncryptedValue()
    {
        await _database.SeedAccountAsync();
        var handler = CreateHandler();

        await handler.Handle(Command(nationalId: SyntheticNationalId), CancellationToken.None);

        var account = await LoadAsync();
        _encryption.DecryptString(account.NationalIdCiphertext!).Should().Be(SyntheticNationalId);
    }

    [Fact]
    public async Task Handle_EmptyNationalId_ClearsStoredValue()
    {
        await _database.SeedAccountAsync(a => a.SetNationalId(_encryption.EncryptString(SyntheticNationalId)));
        var handler = CreateHandler();

        await handler.Handle(Command(nationalId: string.Empty), CancellationToken.None);

        var account = await LoadAsync();
        account.NationalIdCiphertext.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NullNationalId_LeavesStoredValueUnchanged()
    {
        var existing = _encryption.EncryptString(SyntheticNationalId);
        await _database.SeedAccountAsync(a => a.SetNationalId(existing));
        var handler = CreateHandler();

        await handler.Handle(Command(firstName: "Jane"), CancellationToken.None);

        var account = await LoadAsync();
        account.NationalIdCiphertext.Should().Equal(existing);
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsInvalidOperation()
    {
        var handler = CreateHandler();

        var act = () => handler.Handle(
            Command(firstName: "Jane") with { UserId = AccountTestDatabase.MissingUserId },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Account not found");
    }

    public void Dispose() => _database.Dispose();

    private UpdateProfileHandler CreateHandler() => new(_database.Db, _encryption);

    private async Task<UserAccount> LoadAsync()
    {
        using var verify = _database.NewContext();
        return (await verify.UserAccounts.FindAsync(AccountTestDatabase.UserId))!;
    }

    private static UpdateProfileCommand Command(
        string? firstName = null,
        string? lastName = null,
        string? bio = null,
        string? gender = null,
        string? nationality = null,
        string? phone = null,
        DateOnly? dateOfBirth = null,
        string? nationalId = null) =>
        new(AccountTestDatabase.UserId, firstName, lastName, bio, gender, nationality, phone, dateOfBirth, nationalId);
}
