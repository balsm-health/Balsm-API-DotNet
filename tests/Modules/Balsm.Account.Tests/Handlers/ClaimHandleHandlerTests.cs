using Balsm.Account.Application.Commands;
using Balsm.Account.Infrastructure.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.Account.Tests.Handlers;

public sealed class ClaimHandleHandlerTests : IDisposable
{
    private readonly AccountTestDatabase _database = new();

    [Fact]
    public async Task Handle_FreeHandle_ReturnsLowercaseHandle()
    {
        await _database.SeedAccountAsync();
        var handler = new ClaimHandleHandler(_database.Db);

        var result = await handler.Handle(new ClaimHandleCommand(AccountTestDatabase.UserId, "Jane_Doe"), CancellationToken.None);

        result.Should().Be(new ClaimHandleResult("jane_doe"));
    }

    [Fact]
    public async Task Handle_FreeHandle_PersistsActiveReservationForUser()
    {
        await _database.SeedAccountAsync();
        var handler = new ClaimHandleHandler(_database.Db);

        await handler.Handle(new ClaimHandleCommand(AccountTestDatabase.UserId, "Jane_Doe"), CancellationToken.None);

        using var verify = _database.NewContext();
        var reservation = await verify.UsernameReservations.SingleAsync();
        reservation.Should().BeEquivalentTo(new
        {
            HandleNormalized = "jane_doe",
            UserId = AccountTestDatabase.UserId,
            IsActive = true,
        });
    }

    [Fact]
    public async Task Handle_FreeHandle_SetsHandleOnAccount()
    {
        await _database.SeedAccountAsync();
        var handler = new ClaimHandleHandler(_database.Db);

        await handler.Handle(new ClaimHandleCommand(AccountTestDatabase.UserId, "Jane_Doe"), CancellationToken.None);

        using var verify = _database.NewContext();
        var account = await verify.UserAccounts.FindAsync(AccountTestDatabase.UserId);
        account!.Handle.Should().Be("jane_doe");
    }

    [Fact]
    public async Task Handle_TakenHandle_ThrowsHandleConflictWithSuggestions()
    {
        await _database.SeedAccountAsync();
        await _database.SeedReservationAsync("jane_doe", AccountTestDatabase.OtherUserId);
        var handler = new ClaimHandleHandler(_database.Db);

        var act = () => handler.Handle(new ClaimHandleCommand(AccountTestDatabase.UserId, "Jane_Doe"), CancellationToken.None);

        (await act.Should().ThrowAsync<HandleConflictException>())
            .Which.Suggestions.Should().Equal("jane_doe_1", "jane_doe_2", "jane_doe_3");
    }

    [Fact]
    public async Task Handle_TakenHandleAndTakenSuggestion_OmitsTakenSuggestion()
    {
        await _database.SeedAccountAsync();
        await _database.SeedReservationAsync("jane_doe", AccountTestDatabase.OtherUserId);
        await _database.SeedReservationAsync("jane_doe_2", AccountTestDatabase.OtherUserId);
        var handler = new ClaimHandleHandler(_database.Db);

        var act = () => handler.Handle(new ClaimHandleCommand(AccountTestDatabase.UserId, "jane_doe"), CancellationToken.None);

        (await act.Should().ThrowAsync<HandleConflictException>())
            .Which.Suggestions.Should().Equal("jane_doe_1", "jane_doe_3");
    }

    [Fact]
    public async Task Handle_TakenHandle_DoesNotChangeAccountHandle()
    {
        await _database.SeedAccountAsync();
        await _database.SeedReservationAsync("jane_doe", AccountTestDatabase.OtherUserId);
        var handler = new ClaimHandleHandler(_database.Db);

        var act = () => handler.Handle(new ClaimHandleCommand(AccountTestDatabase.UserId, "jane_doe"), CancellationToken.None);

        await act.Should().ThrowAsync<HandleConflictException>();
        using var verify = _database.NewContext();
        var account = await verify.UserAccounts.FindAsync(AccountTestDatabase.UserId);
        account!.Handle.Should().BeNull();
    }

    public void Dispose() => _database.Dispose();
}
