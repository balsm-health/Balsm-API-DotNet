using Balsm.Account.Application.Commands;
using Balsm.Account.Infrastructure.Handlers;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Handlers;

public sealed class ChangeCountryHandlerTests : IDisposable
{
    private readonly AccountTestDatabase _database = new();

    [Fact]
    public async Task Handle_ExistingAccount_PersistsNewCountry()
    {
        await _database.SeedAccountAsync();
        var handler = new ChangeCountryHandler(_database.Db);

        await handler.Handle(new ChangeCountryCommand(AccountTestDatabase.UserId, "AE"), CancellationToken.None);

        using var verify = _database.NewContext();
        var account = await verify.UserAccounts.FindAsync(AccountTestDatabase.UserId);
        account!.CountryCode.Should().Be("AE");
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsInvalidOperation()
    {
        var handler = new ChangeCountryHandler(_database.Db);

        var act = () => handler.Handle(new ChangeCountryCommand(AccountTestDatabase.MissingUserId, "AE"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Account not found");
    }

    public void Dispose() => _database.Dispose();
}
