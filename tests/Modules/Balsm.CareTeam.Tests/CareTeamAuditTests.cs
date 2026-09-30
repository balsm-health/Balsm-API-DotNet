using System.Security.Cryptography;
using Balsm.CareTeam.Application.Commands;
using Balsm.CareTeam.Application.Queries;
using Balsm.CareTeam.Infrastructure.Data;
using Balsm.CareTeam.Infrastructure.Handlers;
using Balsm.Infrastructure.Encryption;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Balsm.CareTeam.Tests;

public sealed class CareTeamAuditTests : IDisposable
{
    private readonly CareTeamDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly CareTeamEncryptionService _crypto;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _profileId = Guid.NewGuid();

    public CareTeamAuditTests()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var opts = new DbContextOptionsBuilder<CareTeamDbContext>().UseSqlite(_connection).Options;
        _db = new CareTeamDbContext(opts, dispatcher);
        _db.Database.EnsureCreated();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CareTeamEncryption:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        _crypto = new CareTeamEncryptionService(config, NullLogger<CareTeamEncryptionService>.Instance);
    }

    private Task Seed(string name = "Provider Alpha") =>
        new UpsertCareProviderHandler(_db, _crypto).Handle(
            new UpsertCareProviderCommand(Guid.NewGuid(), _userId, _profileId, "doctor", name,
                null, null, null, null, null, null, null, null, DateTime.UtcNow),
            CancellationToken.None);

    [Fact]
    public async Task Pull_ThatDecryptsRows_WritesOneAuditRow()
    {
        await Seed();

        await new PullCareProvidersHandler(_db, _crypto).Handle(
            new PullCareProvidersQuery(_userId, _profileId, null, "user:abc", "203.0.113.7", "corr-1"),
            CancellationToken.None);

        var audit = await _db.CareTeamAuditLogs.SingleAsync();
        audit.Actor.Should().Be("user:abc");
        audit.SourceIp.Should().Be("203.0.113.7");
        audit.CorrelationId.Should().Be("corr-1");
        audit.RowCount.Should().Be(1);
        audit.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task Pull_ThatDecryptsNothing_WritesNoAuditRow()
    {
        await new PullCareProvidersHandler(_db, _crypto).Handle(
            new PullCareProvidersQuery(_userId, _profileId, null, "user:abc", "203.0.113.7", "corr-1"),
            CancellationToken.None);

        (await _db.CareTeamAuditLogs.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Pull_RecordsRowCountNotRowContent()
    {
        await Seed("Provider Alpha");
        await Seed("Provider Beta");

        await new PullCareProvidersHandler(_db, _crypto).Handle(
            new PullCareProvidersQuery(_userId, _profileId, null, "user:abc", "203.0.113.7", "corr-1"),
            CancellationToken.None);

        var audit = await _db.CareTeamAuditLogs.SingleAsync();
        audit.RowCount.Should().Be(2);
        // The audit trail must never become a second copy of the PHI it guards.
        var columns = typeof(Domain.Entities.CareTeamAuditLog).GetProperties().Select(p => p.Name);
        columns.Should().NotContain(["Name", "Phone", "Email", "Notes"]);
    }

    [Fact]
    public async Task Pull_EachCall_AppendsAnotherAuditRow()
    {
        await Seed();
        var handler = new PullCareProvidersHandler(_db, _crypto);
        var query = new PullCareProvidersQuery(_userId, _profileId, null, "user:abc", "203.0.113.7", "corr-1");

        await handler.Handle(query, CancellationToken.None);
        await handler.Handle(query, CancellationToken.None);

        (await _db.CareTeamAuditLogs.CountAsync()).Should().Be(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
