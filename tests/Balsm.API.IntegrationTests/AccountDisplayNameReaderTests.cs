using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Account.Infrastructure.Services;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.API.IntegrationTests;

/// <summary>The non-PHI names contract family linking shows: handle and display name, nothing else.</summary>
public sealed class AccountDisplayNameReaderTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AccountDbContext _db;

    public AccountDisplayNameReaderTests()
    {
        _connection.Open();
        _db = new AccountDbContext(
            new DbContextOptionsBuilder<AccountDbContext>().UseSqlite(_connection).Options,
            new NoEvents());
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class NoEvents : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private async Task<Guid> AddAsync(string? handle, string? first, string? last, bool deleted = false)
    {
        var account = UserAccount.Create("EG", "en");
        if (handle is not null) account.ClaimHandle(handle);
        if (first is not null || last is not null) account.UpdateProfile(firstName: first, lastName: last);
        if (deleted) account.RequestDeletion(new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        _db.UserAccounts.Add(account);
        await _db.SaveChangesAsync();
        return account.Id;
    }

    [Fact]
    public async Task GetAsync_ReturnsHandleAndComposedDisplayName()
    {
        var id = await AddAsync("member_one", "Jane", "Doe");

        var names = await new AccountDisplayNameReader(_db).GetAsync([id]);

        names[id].Should().Be(new Balsm.SharedKernel.Contracts.AccountDisplayName("member_one", "Jane Doe"));
    }

    [Fact]
    public async Task GetAsync_AccountWithoutNames_MapsToNulls()
    {
        var id = await AddAsync(null, null, null);

        var names = await new AccountDisplayNameReader(_db).GetAsync([id, id]);

        names.Should().ContainSingle().Which.Value.Should().Be(new Balsm.SharedKernel.Contracts.AccountDisplayName(null, null));
    }

    [Fact]
    public async Task GetAsync_UnknownAndDeletedAccounts_AreAbsent()
    {
        var deleted = await AddAsync("gone", "Jane", "Doe", deleted: true);

        var names = await new AccountDisplayNameReader(_db).GetAsync([deleted, Guid.NewGuid()]);

        names.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_NoIds_ReturnsEmptyWithoutQuerying()
    {
        var names = await new AccountDisplayNameReader(_db).GetAsync([]);

        names.Should().BeEmpty();
    }
}
