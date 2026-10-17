using Xunit;
using Balsm.CareDirectory.Application.Queries;
using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.Handlers;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// The offline catalogue behind GET /care/packs.
///
/// Backed by a table the nightly job writes rather than a committed file: once
/// places rebuild nightly, a file would need an API redeploy every night.
/// </summary>
public sealed class MapPackCatalogueTests : IDisposable
{
    private readonly CareDirectoryDbContext _db;
    private readonly SqliteConnection _connection;

    public MapPackCatalogueTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new CareDirectoryDbContext(
            new DbContextOptionsBuilder<CareDirectoryDbContext>().UseSqlite(_connection).Options,
            Substitute.For<IDomainEventDispatcher>());
        _db.Database.EnsureCreated();
    }

    private const string Digest = "0372f6996c9435ff7e98d774aa11bb22cc33dd44ee55ff66007788990011aabb";

    private static MapPackArtifact Basemap(string id, string version = "20260913") =>
        MapPackArtifact.Publish(id, "Cairo", "القاهرة", MapPackArtifactKind.Basemap, version,
            27_145_146, Digest, $"https://cdn.balsm.health/packs/{id}-{version}.pmtiles",
            31.21, 29.75, 31.91, 30.32);

    private static MapPackArtifact Places(string id, string version = "20260914", int count = 10_920) =>
        MapPackArtifact.Publish(id, "Cairo", "القاهرة", MapPackArtifactKind.Places, version,
            1_051_648, Digest, $"https://cdn.balsm.health/places/{id}-{version}.json.gz",
            31.21, 29.75, 31.91, 30.32, placeCount: count);

    private Task<IReadOnlyList<MapPackDto>> Catalogue(string? lang = null) =>
        new GetMapPacksHandler(_db).Handle(new GetMapPacksQuery(lang), default);

    [Fact]
    public async Task AGovernorateWithBothArtifactsIsOffered()
    {
        _db.MapPackArtifacts.AddRange(Basemap("cairo"), Places("cairo"));
        await _db.SaveChangesAsync();

        var pack = Assert.Single(await Catalogue());

        Assert.Equal("cairo", pack.Id);
        Assert.Equal("Cairo", pack.Name);
        Assert.Equal([31.21, 29.75, 31.91, 30.32], pack.Bounds);
        Assert.Equal(10_920, pack.Places.Count);
        Assert.Null(pack.Basemap.Count);
    }

    [Fact]
    public async Task NameDefaultsToEnglishWhenLangIsOmittedOrUnrecognised()
    {
        _db.MapPackArtifacts.AddRange(Basemap("cairo"), Places("cairo"));
        await _db.SaveChangesAsync();

        Assert.Equal("Cairo", Assert.Single(await Catalogue(lang: null)).Name);
        Assert.Equal("Cairo", Assert.Single(await Catalogue(lang: "fr")).Name);
    }

    [Fact]
    public async Task ArabicIsReturnedWhenRequested()
    {
        _db.MapPackArtifacts.AddRange(Basemap("cairo"), Places("cairo"));
        await _db.SaveChangesAsync();

        Assert.Equal("القاهرة", Assert.Single(await Catalogue(lang: "ar")).Name);
        // Case-insensitive — the app should not have to get this exactly right.
        Assert.Equal("القاهرة", Assert.Single(await Catalogue(lang: "AR")).Name);
    }

    [Fact]
    public async Task TheTwoArtifactsCarryIndependentVersions()
    {
        // The whole point of the split: a nightly places build must not
        // invalidate a 25MB basemap the user already holds.
        _db.MapPackArtifacts.AddRange(Basemap("cairo", "20260901"), Places("cairo", "20260914"));
        await _db.SaveChangesAsync();

        var pack = Assert.Single(await Catalogue());

        Assert.Equal("20260901", pack.Basemap.Version);
        Assert.Equal("20260914", pack.Places.Version);
    }

    [Fact]
    public async Task AGovernorateMissingItsPlacesIsNotOffered()
    {
        // A basemap with no places is a street map with no pharmacies on it —
        // not something to advertise as a download.
        _db.MapPackArtifacts.Add(Basemap("cairo"));
        await _db.SaveChangesAsync();

        Assert.Empty(await Catalogue());
    }

    [Fact]
    public async Task AGovernorateMissingItsBasemapIsNotOffered()
    {
        _db.MapPackArtifacts.Add(Places("cairo"));
        await _db.SaveChangesAsync();

        Assert.Empty(await Catalogue());
    }

    [Fact]
    public async Task AnEmptyCatalogueIsEmptyRatherThanAnError()
    {
        // Before the first nightly run there is nothing to offer, and that is
        // an ordinary state — the app shows no packs, not a failure.
        Assert.Empty(await Catalogue());
    }

    [Fact]
    public async Task OneRowPerGovernoratePerKind()
    {
        // Without the unique index a retried nightly run would publish a second
        // Cairo places row and the endpoint would return Cairo twice.
        _db.MapPackArtifacts.AddRange(Basemap("cairo"), Places("cairo"));
        await _db.SaveChangesAsync();

        _db.MapPackArtifacts.Add(Places("cairo", "20260915"));

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public void APlacesArtifactMustCarryItsCount()
    {
        Assert.Throws<ArgumentException>(() =>
            MapPackArtifact.Publish("cairo", "Cairo", "القاهرة", MapPackArtifactKind.Places,
                "20260914", 1024, Digest, "https://cdn.balsm.health/x", 0, 0, 1, 1));
    }

    [Fact]
    public void AMalformedDigestIsRejectedAtPublish()
    {
        // The app rejects a download whose checksum does not match, so a bad
        // digest here would fail every install rather than none.
        Assert.Throws<ArgumentException>(() =>
            MapPackArtifact.Publish("cairo", "Cairo", "القاهرة", MapPackArtifactKind.Basemap,
                "20260913", 1024, "tooshort", "https://cdn.balsm.health/x", 0, 0, 1, 1));
    }

    [Fact]
    public void AnEmptyArtifactIsNotPublishable()
    {
        Assert.Throws<ArgumentException>(() =>
            MapPackArtifact.Publish("cairo", "Cairo", "القاهرة", MapPackArtifactKind.Basemap,
                "20260913", 0, Digest, "https://cdn.balsm.health/x", 0, 0, 1, 1));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
