using Balsm.Account.Application.Commands;
using Balsm.Account.Infrastructure.Handlers;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Handlers;

public sealed class ChangeLanguageHandlerTests : IDisposable
{
    private readonly AccountTestDatabase _database = new();

    [Fact]
    public async Task Handle_ExistingAccount_PersistsNewLanguage()
    {
        await _database.SeedAccountAsync();
        var handler = new ChangeLanguageHandler(_database.Db);

        await handler.Handle(new ChangeLanguageCommand(AccountTestDatabase.UserId, "ar"), CancellationToken.None);

        using var verify = _database.NewContext();
        var account = await verify.UserAccounts.FindAsync(AccountTestDatabase.UserId);
        account!.PreferredLanguage.Should().Be("ar");
    }

    [Fact]
    public async Task Handle_AccountNotFound_ThrowsInvalidOperation()
    {
        var handler = new ChangeLanguageHandler(_database.Db);

        var act = () => handler.Handle(new ChangeLanguageCommand(AccountTestDatabase.MissingUserId, "ar"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Account not found");
    }

    public void Dispose() => _database.Dispose();
}
