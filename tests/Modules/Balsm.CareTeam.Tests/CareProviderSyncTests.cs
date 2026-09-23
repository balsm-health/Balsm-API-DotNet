using System.Security.Cryptography;
using Balsm.CareTeam.Application.Commands;
using Balsm.CareTeam.Application.Queries;
using Balsm.CareTeam.Domain;
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

public sealed class CareProviderSyncTests : IDisposable
{
    private readonly CareTeamDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly CareTeamEncryptionService _crypto;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _profileId = Guid.NewGuid();

    public CareProviderSyncTests()
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

    private UpsertCareProviderCommand Upsert(Guid id, string name = "Provider Alpha", string type = "doctor") =>
        new(id, _userId, _profileId, type, name,
            null, null, null, null, null, null, null, null, DateTime.UtcNow);

    [Fact]
    public async Task Upsert_NewId_CreatesRow()
    {
        var id = Guid.NewGuid();
        var result = await new UpsertCareProviderHandler(_db, _crypto)
            .Handle(Upsert(id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _db.CareProviders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Upsert_SameIdTwice_IsIdempotent_NoDuplicateRow()
    {
        var id = Guid.NewGuid();
        var handler = new UpsertCareProviderHandler(_db, _crypto);
        await handler.Handle(Upsert(id), CancellationToken.None);
        await handler.Handle(Upsert(id), CancellationToken.None);

        (await _db.CareProviders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Upsert_ExistingId_OverwritesFields()
    {
        var id = Guid.NewGuid();
        var handler = new UpsertCareProviderHandler(_db, _crypto);
        await handler.Handle(Upsert(id, "Provider Alpha"), CancellationToken.None);
        await handler.Handle(Upsert(id, "Provider Beta", "pharmacy"), CancellationToken.None);

        var pull = await new PullCareProvidersHandler(_db, _crypto)
            .Handle(new PullCareProvidersQuery(_userId, _profileId, null), CancellationToken.None);

        pull.Value!.Single().Name.Should().Be("Provider Beta");
        pull.Value!.Single().Type.Should().Be("pharmacy");
    }

    /// <summary>Review Focus 1 — a device with a clock months ahead must not
    /// be able to freeze a row by supplying its own updated_at.</summary>
    [Fact]
    public async Task Upsert_UsesServerClockForUpdatedAt_NotClientCreatedAt()
    {
        var id = Guid.NewGuid();
        var handler = new UpsertCareProviderHandler(_db, _crypto);
        var farFuture = DateTime.UtcNow.AddYears(5);
        await handler.Handle(
            new UpsertCareProviderCommand(id, _userId, _profileId, "doctor", "Provider Alpha",
                null, null, null, null, null, null, null, null, farFuture),
            CancellationToken.None);
        // Second write is what stamps UpdatedAt.
        await handler.Handle(Upsert(id, "Provider Beta"), CancellationToken.None);
        _db.ChangeTracker.Clear();

        var row = await _db.CareProviders.SingleAsync(p => p.Id == id);
        row.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    /// <summary>Review Focus 5 — a stale device must not resurrect a tombstone.</summary>
    [Fact]
    public async Task Upsert_OnTombstonedId_ReturnsTombstonedError()
    {
        var id = Guid.NewGuid();
        await new UpsertCareProviderHandler(_db, _crypto).Handle(Upsert(id), CancellationToken.None);
        await new DeleteCareProviderHandler(_db).Handle(
            new DeleteCareProviderCommand(id, _userId), CancellationToken.None);

        var result = await new UpsertCareProviderHandler(_db, _crypto)
            .Handle(Upsert(id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CareTeamErrors.Tombstoned);
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        var result = await new DeleteCareProviderHandler(_db)
            .Handle(new DeleteCareProviderCommand(Guid.NewGuid(), _userId), CancellationToken.None);

        result.Error.Should().Be(CareTeamErrors.NotFound);
    }

    [Fact]
    public async Task Delete_IsIdempotent()
    {
        var id = Guid.NewGuid();
        await new UpsertCareProviderHandler(_db, _crypto).Handle(Upsert(id), CancellationToken.None);
        var handler = new DeleteCareProviderHandler(_db);

        (await handler.Handle(new DeleteCareProviderCommand(id, _userId), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await handler.Handle(new DeleteCareProviderCommand(id, _userId), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Pull_IncludesTombstones()
    {
        var id = Guid.NewGuid();
        await new UpsertCareProviderHandler(_db, _crypto).Handle(Upsert(id), CancellationToken.None);
        await new DeleteCareProviderHandler(_db).Handle(
            new DeleteCareProviderCommand(id, _userId), CancellationToken.None);

        var pull = await new PullCareProvidersHandler(_db, _crypto)
            .Handle(new PullCareProvidersQuery(_userId, _profileId, null), CancellationToken.None);

        pull.Value!.Single().IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Pull_WithSinceCursor_ReturnsOnlyNewerRows()
    {
        var handler = new UpsertCareProviderHandler(_db, _crypto);
        await handler.Handle(Upsert(Guid.NewGuid(), "Provider Alpha"), CancellationToken.None);
        await Task.Delay(50);
        var cursor = DateTime.UtcNow;
        await Task.Delay(50);
        await handler.Handle(Upsert(Guid.NewGuid(), "Provider Beta"), CancellationToken.None);

        var pull = await new PullCareProvidersHandler(_db, _crypto)
            .Handle(new PullCareProvidersQuery(_userId, _profileId, cursor), CancellationToken.None);

        pull.Value!.Should().ContainSingle().Which.Name.Should().Be("Provider Beta");
    }

    /// <summary>Review Focus 4 — a dependant's roster must not leak into the
    /// self profile's pull.</summary>
    [Fact]
    public async Task Pull_ScopedToHealthProfile_ExcludesOtherProfiles()
    {
        var otherProfile = Guid.NewGuid();
        var handler = new UpsertCareProviderHandler(_db, _crypto);
        await handler.Handle(Upsert(Guid.NewGuid(), "Provider Alpha"), CancellationToken.None);
        await handler.Handle(
            new UpsertCareProviderCommand(Guid.NewGuid(), _userId, otherProfile, "doctor", "Dependant Provider",
                null, null, null, null, null, null, null, null, DateTime.UtcNow),
            CancellationToken.None);

        var pull = await new PullCareProvidersHandler(_db, _crypto)
            .Handle(new PullCareProvidersQuery(_userId, _profileId, null), CancellationToken.None);

        pull.Value!.Should().ContainSingle().Which.Name.Should().Be("Provider Alpha");
    }

    /// <summary>Review Focus 4 — another user's id must answer NotFound, never
    /// a distinguishable Forbidden, so ownership cannot be probed.</summary>
    [Fact]
    public async Task Delete_AnotherUsersId_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        await new UpsertCareProviderHandler(_db, _crypto).Handle(Upsert(id), CancellationToken.None);

        var result = await new DeleteCareProviderHandler(_db)
            .Handle(new DeleteCareProviderCommand(id, Guid.NewGuid()), CancellationToken.None);

        result.Error.Should().Be(CareTeamErrors.NotFound);
    }

    /// <summary>Another user's id on upsert must also answer NotFound, not
    /// silently overwrite the owner's row.</summary>
    [Fact]
    public async Task Upsert_AnotherUsersId_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        await new UpsertCareProviderHandler(_db, _crypto).Handle(Upsert(id), CancellationToken.None);

        var result = await new UpsertCareProviderHandler(_db, _crypto).Handle(
            new UpsertCareProviderCommand(id, Guid.NewGuid(), _profileId, "doctor", "Hijacked",
                null, null, null, null, null, null, null, null, DateTime.UtcNow),
            CancellationToken.None);

        result.Error.Should().Be(CareTeamErrors.NotFound);
    }

    [Fact]
    public async Task Upsert_InvalidType_ReturnsInvalidTypeError()
    {
        var result = await new UpsertCareProviderHandler(_db, _crypto)
            .Handle(Upsert(Guid.NewGuid(), "Provider Alpha", "astrologer"), CancellationToken.None);

        result.Error.Should().Be(CareTeamErrors.InvalidType);
    }

    [Fact]
    public async Task Pull_RoundTripsEveryOptionalField()
    {
        var id = Guid.NewGuid();
        await new UpsertCareProviderHandler(_db, _crypto).Handle(
            new UpsertCareProviderCommand(id, _userId, _profileId, "clinic", "Provider Alpha",
                "Cardiology", "+201000000001", "+201000000002", "alpha@example.test",
                "Clinic Beta", "12 Example Street", "https://maps.example.test/x", "Mornings only",
                DateTime.UtcNow),
            CancellationToken.None);

        var row = (await new PullCareProvidersHandler(_db, _crypto)
            .Handle(new PullCareProvidersQuery(_userId, _profileId, null), CancellationToken.None)).Value!.Single();

        row.Specialty.Should().Be("Cardiology");
        row.Phone.Should().Be("+201000000001");
        row.Phone2.Should().Be("+201000000002");
        row.Email.Should().Be("alpha@example.test");
        row.Clinic.Should().Be("Clinic Beta");
        row.Address.Should().Be("12 Example Street");
        row.MapUrl.Should().Be("https://maps.example.test/x");
        row.Notes.Should().Be("Mornings only");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
