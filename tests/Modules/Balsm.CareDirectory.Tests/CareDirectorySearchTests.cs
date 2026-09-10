using Balsm.CareDirectory.Application.Queries;
using Balsm.CareDirectory.Domain;
using Balsm.CareDirectory.Domain.Entities;
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
/// GET /care/entities behaviour: distance sort, type filter, radius cutoff, and
/// free-text search across both scripts.
///
/// Every test seeds its own rows. These used to assert against the 12-row HasData
/// seed — production reference data — so a change to that seed broke unrelated
/// tests, and the tests in turn made the fabricated seed load-bearing. Fixture
/// names are deliberately unmistakable as fixtures: the seed's mistake was
/// dressing invented contact details in real institutions' names.
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

    private async Task SeedAsync(params CarePlace[] places)
    {
        _db.CarePlaces.AddRange(places);
        await _db.SaveChangesAsync();
    }

    private static CarePlace Place(
        string type,
        string nameEn,
        double lat,
        double lng,
        string? addressEn = "Fixture Street",
        string? nameAr = null,
        string? addressAr = null) =>
        CarePlace.Create(
            type: type,
            nameEn: nameEn,
            nameAr: nameAr,
            addressEn: addressEn,
            addressAr: addressAr,
            lat: lat,
            lng: lng,
            countryCode: "EG",
            source: "overture",
            externalId: $"fixture-{Guid.NewGuid():N}",
            confidence: 0.9);

    [Fact]
    public async Task Search_SortsByDistanceAscending()
    {
        await SeedAsync(
            Place("hospital", "Fixture Hospital North", 30.1300, 31.2830),
            Place("pharmacy", "Fixture Pharmacy At Origin", OriginLat, OriginLng));

        var result = await new SearchNearbyHandler(_db).Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, null, null, null),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.DistanceKm).Should().BeInAscendingOrder("results are sorted by distance_km");
        result[0].NameEn.Should().Be("Fixture Pharmacy At Origin");
        result[0].DistanceKm.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public async Task Search_FiltersByType()
    {
        await SeedAsync(
            Place("pharmacy", "Fixture Pharmacy One", OriginLat, OriginLng),
            Place("pharmacy", "Fixture Pharmacy Two", 30.0570, 31.2110),
            Place("hospital", "Fixture Hospital", 30.1300, 31.2830));

        var result = await new SearchNearbyHandler(_db).Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, null, "pharmacy", null),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.Type == "pharmacy");
        result.Select(r => r.DistanceKm).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Search_FiltersByRadius()
    {
        await SeedAsync(
            Place("pharmacy", "Fixture Near", OriginLat, OriginLng),
            Place("hospital", "Fixture Far", 31.2000, 29.9000));

        var handler = new SearchNearbyHandler(_db);

        var withinFar = await handler.Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, 1000.0, null, null), CancellationToken.None);
        var withinNear = await handler.Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, 2.0, null, null), CancellationToken.None);

        withinFar.Should().HaveCount(2, "a 1000 km radius covers both fixtures");
        withinNear.Should().HaveCount(1, "a 2 km radius excludes the outlying fixture");
        withinNear.Should().OnlyContain(r => r.DistanceKm <= 2.0);
    }

    [Fact]
    public async Task Search_FiltersByFreeTextOnLatinName()
    {
        await SeedAsync(
            Place("clinic", "Fixture Zamalek Clinic", 30.0614, 31.2197, addressEn: "Zamalek, Cairo"),
            Place("pharmacy", "Fixture Dokki Pharmacy", 30.0384, 31.2119, addressEn: "Dokki, Giza"));

        var result = await new SearchNearbyHandler(_db).Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, null, null, "Zamalek"),
            CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].NameEn.Should().Be("Fixture Zamalek Clinic");
    }

    [Fact]
    public async Task Search_MatchesArabicAcrossHamzaSpellings()
    {
        // Stored WITHOUT the hamza, searched WITH it. Egyptian listings use both
        // spellings freely, so raw comparison would return nothing here.
        await SeedAsync(Place("scan", "Fixture Imaging Centre", OriginLat, OriginLng,
            nameAr: "مركز الاشعه التشخيصيه"));

        var result = await new SearchNearbyHandler(_db).Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, null, null, "أشعة"),
            CancellationToken.None);

        result.Should().HaveCount(1, "normalised search folds أ to ا and ة to ه");
    }

    [Fact]
    public async Task Search_ReturnsNullsRatherThanInventedValues()
    {
        await SeedAsync(Place("pharmacy", "Fixture Pharmacy", OriginLat, OriginLng));

        var result = await new SearchNearbyHandler(_db).Handle(
            new SearchNearbyQuery(OriginLat, OriginLng, null, null, null),
            CancellationToken.None);

        // Overture has no hours or ratings field, and one name per place. The wire
        // format must say "absent", never fill the gap with a fabricated value.
        result[0].Hours.Should().BeNull();
        result[0].Rating.Should().BeNull();
        result[0].Phone.Should().BeNull();
        result[0].NameAr.Should().BeNull();
    }

    [Fact]
    public void Create_RejectsAPlaceWithNoNameInEitherScript()
    {
        var act = () => CarePlace.Create(
            type: "clinic", nameEn: null, nameAr: null,
            addressEn: null, addressAr: null,
            lat: OriginLat, lng: OriginLng, countryCode: "EG", source: "overture");

        act.Should().Throw<ArgumentException>("a place with no name at all is unrenderable");
    }

    [Fact]
    public async Task Create_PopulatesNormalisedArabicColumns()
    {
        await SeedAsync(Place("lab", "Fixture Lab", OriginLat, OriginLng, nameAr: "معمل مستشفى"));

        var row = await _db.CarePlaces.SingleAsync();
        row.NameArNorm.Should().Be(ArabicText.Normalize("معمل مستشفى"));
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
        _connection.Dispose();
    }
}
