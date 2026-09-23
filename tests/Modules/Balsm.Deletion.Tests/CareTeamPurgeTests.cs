using Balsm.CareTeam.Domain.Entities;
using Balsm.CareTeam.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Jobs;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Balsm.Deletion.Tests;

/// <summary>
/// FR-512: account deletion must leave zero care-team rows, tombstones included.
/// The purge job hardcodes every context — a module whose data is not purged here
/// survives an account deletion silently.
/// </summary>
public sealed class CareTeamPurgeTests : IDisposable
{
    private readonly CareTeamDbContext _db;
    private readonly SqliteConnection _connection;

    public CareTeamPurgeTests()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var opts = new DbContextOptionsBuilder<CareTeamDbContext>().UseSqlite(_connection).Options;
        _db = new CareTeamDbContext(opts, dispatcher);
        _db.Database.EnsureCreated();
    }

    private static CareProviderFields Fields() => new([1], null, null, null, null, null, null, null, null);

    [Fact]
    public async Task PurgeCareTeam_RemovesLiveAndTombstonedRowsForThatUserOnly()
    {
        var doomed = Guid.NewGuid();
        var survivor = Guid.NewGuid();

        var live = CareProvider.Create(Guid.NewGuid(), doomed, Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow);
        var dead = CareProvider.Create(Guid.NewGuid(), doomed, Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow);
        dead.Tombstone();
        var other = CareProvider.Create(Guid.NewGuid(), survivor, Guid.NewGuid(), "doctor", Fields(), DateTime.UtcNow);
        _db.CareProviders.AddRange(live, dead, other);
        await _db.SaveChangesAsync();

        await DeletionPurgeJob.PurgeCareTeamAsync(_db, doomed, CancellationToken.None);

        (await _db.CareProviders.IgnoreQueryFilters().Where(p => p.UserId == doomed).CountAsync())
            .Should().Be(0);
        (await _db.CareProviders.IgnoreQueryFilters().Where(p => p.UserId == survivor).CountAsync())
            .Should().Be(1);
    }

    [Fact]
    public async Task PurgeCareTeam_RemovesTheAuditTrailToo()
    {
        var doomed = Guid.NewGuid();
        var survivor = Guid.NewGuid();
        _db.CareTeamAuditLogs.AddRange(
            CareTeamAuditLog.Record(doomed, Guid.NewGuid(), "user:a", "203.0.113.7", "corr-1", 3),
            CareTeamAuditLog.Record(survivor, Guid.NewGuid(), "user:b", "203.0.113.8", "corr-2", 1));
        await _db.SaveChangesAsync();

        await DeletionPurgeJob.PurgeCareTeamAsync(_db, doomed, CancellationToken.None);

        (await _db.CareTeamAuditLogs.Where(a => a.UserId == doomed).CountAsync()).Should().Be(0);
        (await _db.CareTeamAuditLogs.Where(a => a.UserId == survivor).CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PurgeCareTeam_OnAccountWithNoRows_IsANoOp()
    {
        await DeletionPurgeJob.PurgeCareTeamAsync(_db, Guid.NewGuid(), CancellationToken.None);

        (await _db.CareProviders.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
