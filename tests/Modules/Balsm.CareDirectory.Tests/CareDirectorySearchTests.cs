using Balsm.CareDirectory.Application.Queries;
using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.Handlers;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// GET /care/entities behavior: EnsureCreated applies the 12-row HasData seed;
/// the handler returns all seeded rows sorted ascending by distance and filters
/// by type / radius.
/// </summary>
public sealed class CareDirectorySearchTests : IDisposable
{
    // Tahrir Square, Cairo — origin used for the distance sort.
    private const double OriginLat = 30.0444;
    private const double OriginLng = 31.2357;

    private readonly CareDirectoryDbContext _db;
    private readonly SqliteConnection _connection;

    public CareDirectorySearchTests()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();

        // A :memory: SQLite database lives only as long as a connection is open,
        // so keep one open for the fixture lifetime.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var opts = new DbContextOptionsBuilder<CareDirectoryDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new CareDirectoryDbContext(opts, dispatcher);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Search_ReturnsAll12Seeded_SortedByDistanceAscending()
    {
        var handler = new SearchNearbyHandler(_db);

        var result = await handler.Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, null, null, null),
            CancellationToken.None);

        result.Should().HaveCount(12, "all 12 HasData rows are seeded by EnsureCreated");
        result.Select(r => r.DistanceKm).Should().BeInAscendingOrder("results are sorted by distance_km");
        // The seed at the exact origin (El-Ezaby Pharmacy) must be first.
        result[0].NameEn.Should().Be("El-Ezaby Pharmacy");
        result[0].DistanceKm.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public async Task Search_FiltersByType()
    {
        var handler = new SearchNearbyHandler(_db);

        var result = await handler.Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, null, "pharmacy", null),
            CancellationToken.None);

        result.Should().HaveCount(2, "two pharmacies are seeded (El-Ezaby, Seif)");
        result.Should().OnlyContain(r => r.Type == "pharmacy");
        result.Select(r => r.DistanceKm).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Search_FiltersByRadius()
    {
        var handler = new SearchNearbyHandler(_db);

        var withinFar = await handler.Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, 1000.0, null, null),
            CancellationToken.None);
        var withinNear = await handler.Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, 2.0, null, null),
            CancellationToken.None);

        withinFar.Should().HaveCount(12, "a 1000 km radius covers every seeded place");
        withinNear.Should().HaveCountLessThan(12, "a 2 km radius excludes the outlying places");
        withinNear.Should().OnlyContain(r => r.DistanceKm <= 2.0);
    }

    [Fact]
    public async Task Search_FiltersByFreeText()
    {
        var handler = new SearchNearbyHandler(_db);

        var result = await handler.Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, null, null, "Zamalek"),
            CancellationToken.None);

        result.Should().OnlyContain(r => r.AddressEn.Contains("Zamalek"));
        result.Select(r => r.NameEn).Should().Contain("Dr. Sara Kamal Clinic");
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
        _connection.Dispose();
    }
}
