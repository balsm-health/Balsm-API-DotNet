using Balsm.Account.Application.Queries;
using Balsm.Account.Infrastructure.Handlers;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Handlers;

public sealed class CheckHandleHandlerTests : IDisposable
{
    private readonly AccountTestDatabase _database = new();

    [Theory]
    [InlineData("ab")]
    [InlineData("jane-doe")]
    [InlineData("jane doe")]
    [InlineData("a_handle_that_is_far_too_long_x")]
    public async Task Handle_InvalidFormat_ReturnsInvalidFormat(string handle)
    {
        var handler = new CheckHandleHandler(_database.Db);

        var result = await handler.Handle(new CheckHandleQuery(handle), CancellationToken.None);

        result.Should().Be(new CheckHandleResult(false, "InvalidFormat"));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Support")]
    public async Task Handle_SeededReservedHandle_ReturnsReserved(string handle)
    {
        var handler = new CheckHandleHandler(_database.Db);

        var result = await handler.Handle(new CheckHandleQuery(handle), CancellationToken.None);

        result.Should().Be(new CheckHandleResult(false, "Reserved"));
    }

    [Fact]
    public async Task Handle_ActiveReservation_ReturnsTaken()
    {
        await _database.SeedReservationAsync("jane_doe", AccountTestDatabase.OtherUserId);
        var handler = new CheckHandleHandler(_database.Db);

        var result = await handler.Handle(new CheckHandleQuery("Jane_Doe"), CancellationToken.None);

        result.Should().Be(new CheckHandleResult(false, "Taken"));
    }

    [Fact]
    public async Task Handle_ReleasedReservation_ReturnsAvailable()
    {
        await _database.SeedReservationAsync("jane_doe", AccountTestDatabase.OtherUserId);
        var reservation = await _database.Db.UsernameReservations.FindAsync("jane_doe");
        reservation!.Release();
        await _database.Db.SaveChangesAsync();
        var handler = new CheckHandleHandler(_database.Db);

        var result = await handler.Handle(new CheckHandleQuery("jane_doe"), CancellationToken.None);

        result.Should().Be(new CheckHandleResult(true, null));
    }

    [Fact]
    public async Task Handle_UnusedValidHandle_ReturnsAvailable()
    {
        var handler = new CheckHandleHandler(_database.Db);

        var result = await handler.Handle(new CheckHandleQuery("jane_doe"), CancellationToken.None);

        result.Should().Be(new CheckHandleResult(true, null));
    }

    public void Dispose() => _database.Dispose();
}
