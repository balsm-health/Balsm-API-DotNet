using Balsm.CareTeam.Domain.Entities;
using Balsm.CareTeam.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Balsm.CareTeam.Tests;

public sealed class CareProviderPersistenceTests : IDisposable
{
    private readonly CareTeamDbContext _db;
    private readonly SqliteConnection _connection;

    public CareProviderPersistenceTests()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        // A :memory: SQLite database dies with its last connection — hold one open.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var opts = new DbContextOptionsBuilder<CareTeamDbContext>().UseSqlite(_connection).Options;
        _db = new CareTeamDbContext(opts, dispatcher);
        _db.Database.EnsureCreated();
    }

    private static CareProviderFields Fields() =>
        new([1, 2, 3], null, null, null, null, null, null, null, null);

    [Fact]
    public async Task SaveAndReload_RoundTripsCiphertext()
    {
        var id = Guid.NewGuid();
        _db.CareProviders.Add(CareProvider.Create(
            id, Guid.NewGuid(), Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var loaded = await _db.CareProviders.SingleAsync(p => p.Id == id);
        loaded.Name.Should().Equal(1, 2, 3);
        loaded.Type.Should().Be("doctor");
    }

    [Fact]
    public async Task TombstonedRow_IsHiddenByDefaultButVisibleWithIgnoreQueryFilters()
    {
        var id = Guid.NewGuid();
        var provider = CareProvider.Create(
            id, Guid.NewGuid(), Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow);
        _db.CareProviders.Add(provider);
        await _db.SaveChangesAsync();

        provider.Tombstone();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        (await _db.CareProviders.AnyAsync(p => p.Id == id)).Should().BeFalse();
        (await _db.CareProviders.IgnoreQueryFilters().AnyAsync(p => p.Id == id)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveChanges_StampsUpdatedAtOnModify()
    {
        var provider = CareProvider.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow);
        _db.CareProviders.Add(provider);
        await _db.SaveChangesAsync();
        provider.UpdatedAt.Should().BeNull();

        provider.Overwrite("pharmacy", Fields());
        await _db.SaveChangesAsync();
        provider.UpdatedAt.Should().NotBeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
