using Balsm.Account.Application.Commands;
using Balsm.Account.Infrastructure.Handlers;
using Balsm.Infrastructure.Encryption;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Handlers;

public sealed class SetDobHandlerTests : IDisposable
{
    private static readonly DateOnly AdultDob = new(1990, 1, 1);
    // Under 18 until 2038 — fixed so the test does not depend on today's date.
    private static readonly DateOnly MinorDob = new(2020, 1, 1);

    private readonly AccountTestDatabase _database = new();
    private readonly DobEncryptionService _encryption = AccountTestDatabase.CreateEncryption();

    [Fact]
    public async Task Handle_AdultDob_PersistsEncryptedDob()
    {
        await _database.SeedAccountAsync();
        var handler = new SetDobHandler(_database.Db, _encryption);

        await handler.Handle(new SetDobCommand(AccountTestDatabase.UserId, AdultDob), CancellationToken.None);

        using var verify = _database.NewContext();
        var account = await verify.UserAccounts.FindAsync(AccountTestDatabase.UserId);
        _encryption.Decrypt(account!.DateOfBirthCiphertext!).Should().Be(AdultDob);
    }

    [Fact]
    public async Task Handle_MinorDob_ThrowsUnderEighteen()
    {
        await _database.SeedAccountAsync();
        var handler = new SetDobHandler(_database.Db, _encryption);

        var act = () => handler.Handle(new SetDobCommand(AccountTestDatabase.UserId, MinorDob), CancellationToken.None);

        await act.Should().ThrowAsync<UnderEighteenException>();
    }

    [Fact]
    public async Task Handle_MinorDob_DoesNotPersistDob()
    {
        await _database.SeedAccountAsync();
        var handler = new SetDobHandler(_database.Db, _encryption);

        var act = () => handler.Handle(new SetDobCommand(AccountTestDatabase.UserId, MinorDob), CancellationToken.None);

        await act.Should().ThrowAsync<UnderEighteenException>();
        using var verify = _database.NewContext();
        var account = await verify.UserAccounts.FindAsync(AccountTestDatabase.UserId);
        account!.DateOfBirthCiphertext.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsInvalidOperation()
    {
        var handler = new SetDobHandler(_database.Db, _encryption);

        var act = () => handler.Handle(new SetDobCommand(AccountTestDatabase.MissingUserId, AdultDob), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Account not found");
    }

    public void Dispose() => _database.Dispose();
}
