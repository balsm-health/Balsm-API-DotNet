using Xunit;
using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.MapPacks;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// One nightly run: reconciling basemap rows against a fake bucket, then
/// exporting and publishing places snapshots. See
/// docs/architecture/c4/care-directory/dynamic-map-pack-export.md for the
/// sequence this exercises.
/// </summary>
public sealed class MapPackExportRunnerTests : IDisposable
{
    private readonly CareDirectoryDbContext _db;
    private readonly SqliteConnection _connection;
    private static readonly string Today = DateTime.UtcNow.ToString("yyyyMMdd");

    private static readonly GovernorateRef Cairo =
        new("cairo", "Cairo", "القاهرة", West: 31.21, South: 29.75, East: 31.91, North: 30.32);

    private static readonly GovernorateRef Giza =
        new("giza", "Giza", "الجيزة", West: 30.5, South: 29.0, East: 31.0, North: 29.7);

    private static readonly MapPackR2Options Options = new() { CdnBaseUrl = "https://cdn.balsm.health" };

    public MapPackExportRunnerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new CareDirectoryDbContext(
            new DbContextOptionsBuilder<CareDirectoryDbContext>().UseSqlite(_connection).Options,
            Substitute.For<IDomainEventDispatcher>());
        _db.Database.EnsureCreated();
    }

    private MapPackExportRunner Runner(FakeObjectStore store, params GovernorateRef[] governorates) =>
        new(_db, store, governorates.Length > 0 ? governorates : [Cairo, Giza], Options, NullLogger.Instance);

    private void AddPlace(string nameEn, double lat, double lng) =>
        _db.CarePlaces.Add(CarePlace.Create(
            "pharmacy", nameEn, "صيدلية", "1 Test St", null, lat, lng, "EG", "overture"));

    [Fact]
    public async Task ANewBasemapInTheBucketIsPublished()
    {
        var store = new FakeObjectStore([new R2Object("packs/cairo-20260913.pmtiles", 27_000_000, Digest)]);

        await Runner(store, Cairo).RunAsync(default);

        var row = Assert.Single(_db.MapPackArtifacts.Where(a => a.Kind == MapPackArtifactKind.Basemap));
        Assert.Equal("cairo", row.GovernorateId);
        Assert.Equal("20260913", row.Version);
        Assert.Equal(27_000_000, row.SizeBytes);
        Assert.Equal(Digest, row.Sha256);
        Assert.Equal("https://cdn.balsm.health/packs/cairo-20260913.pmtiles", row.Url);
    }

    [Fact]
    public async Task ARepublishedBasemapKeepsItsRowIdentityAndUpdatesItsVersion()
    {
        var existing = MapPackArtifact.Publish(
            "cairo", "Cairo", "القاهرة", MapPackArtifactKind.Basemap, "20260901",
            10, Digest, "https://cdn.balsm.health/packs/cairo-20260901.pmtiles", 0, 0, 1, 1);
        _db.MapPackArtifacts.Add(existing);
        await _db.SaveChangesAsync();
        var originalId = existing.Id;

        var store = new FakeObjectStore([new R2Object("packs/cairo-20260913.pmtiles", 28_000_000, Digest)]);
        await Runner(store, Cairo).RunAsync(default);

        var row = Assert.Single(_db.MapPackArtifacts.Where(a => a.Kind == MapPackArtifactKind.Basemap));
        Assert.Equal(originalId, row.Id);
        Assert.Equal("20260913", row.Version);
        Assert.Equal(28_000_000, row.SizeBytes);
    }

    [Fact]
    public async Task ABasemapObjectWithNoSha256MetadataIsSkipped()
    {
        // publish.py always sets this metadata; a key without it was not
        // written by that script and could not be verified on download.
        var store = new FakeObjectStore([new R2Object("packs/cairo-20260913.pmtiles", 27_000_000, Sha256Metadata: null)]);

        await Runner(store, Cairo).RunAsync(default);

        Assert.Empty(_db.MapPackArtifacts.Where(a => a.Kind == MapPackArtifactKind.Basemap));
    }

    [Fact]
    public async Task OnlyTheNewestVersionOfAGovernoratesBasemapIsKept()
    {
        var store = new FakeObjectStore([
            new R2Object("packs/cairo-20260901.pmtiles", 10, Digest),
            new R2Object("packs/cairo-20260913.pmtiles", 20, Digest),
        ]);

        await Runner(store, Cairo).RunAsync(default);

        var row = Assert.Single(_db.MapPackArtifacts.Where(a => a.Kind == MapPackArtifactKind.Basemap));
        Assert.Equal("20260913", row.Version);
    }

    [Fact]
    public async Task PlacesAreBucketedByGovernorateBoundingBox()
    {
        AddPlace("Cairo Pharmacy", lat: 30.0, lng: 31.5);   // inside Cairo
        AddPlace("Giza Pharmacy", lat: 29.3, lng: 30.7);    // inside Giza
        AddPlace("Red Sea Pharmacy", lat: 27.0, lng: 33.0); // inside neither
        await _db.SaveChangesAsync();

        var store = new FakeObjectStore();
        await Runner(store, Cairo, Giza).RunAsync(default);

        var cairoRow = _db.MapPackArtifacts.Single(a => a.GovernorateId == "cairo" && a.Kind == MapPackArtifactKind.Places);
        var gizaRow = _db.MapPackArtifacts.Single(a => a.GovernorateId == "giza" && a.Kind == MapPackArtifactKind.Places);
        Assert.Equal(1, cairoRow.PlaceCount);
        Assert.Equal(1, gizaRow.PlaceCount);
        Assert.Contains(store.Puts, p => p.Key == $"places/cairo-{Today}.ndjson.gz");
        Assert.Contains(store.Puts, p => p.Key == $"places/giza-{Today}.ndjson.gz");
    }

    [Fact]
    public async Task AGovernorateWithNoPriorSnapshotPublishesEvenWithZeroPlaces()
    {
        var store = new FakeObjectStore();
        await Runner(store, Cairo).RunAsync(default);

        var row = Assert.Single(_db.MapPackArtifacts.Where(a => a.Kind == MapPackArtifactKind.Places));
        Assert.Equal(0, row.PlaceCount);
        Assert.Contains(store.Puts, p => p.Key == $"places/cairo-{Today}.ndjson.gz");
    }

    [Fact]
    public async Task ALargeDropFromThePreviousSnapshotIsRefused()
    {
        _db.MapPackArtifacts.Add(MapPackArtifact.Publish(
            "cairo", "Cairo", "القاهرة", MapPackArtifactKind.Places, "20260912",
            1000, Digest, "https://cdn.balsm.health/places/cairo-20260912.ndjson.gz",
            0, 0, 1, 1, placeCount: 100));
        await _db.SaveChangesAsync();

        // Only 10 places today against 100 yesterday — an 80% drop.
        for (var i = 0; i < 10; i++) AddPlace($"Pharmacy {i}", lat: 30.0, lng: 31.5);
        await _db.SaveChangesAsync();

        var store = new FakeObjectStore();
        await Runner(store, Cairo).RunAsync(default);

        var row = _db.MapPackArtifacts.Single(a => a.GovernorateId == "cairo" && a.Kind == MapPackArtifactKind.Places);
        Assert.Equal("20260912", row.Version); // unchanged
        Assert.Equal(100, row.PlaceCount);     // unchanged
        Assert.DoesNotContain(store.Puts, p => p.Key.StartsWith("places/cairo-"));
    }

    [Fact]
    public async Task OneGovernoratesUploadFailureDoesNotStopTheOthers()
    {
        AddPlace("Cairo Pharmacy", lat: 30.0, lng: 31.5);
        AddPlace("Giza Pharmacy", lat: 29.3, lng: 30.7);
        await _db.SaveChangesAsync();

        var store = new FakeObjectStore(throwOnPutKeys: [$"places/cairo-{Today}.ndjson.gz"]);
        await Runner(store, Cairo, Giza).RunAsync(default);

        Assert.Empty(_db.MapPackArtifacts.Where(a => a.GovernorateId == "cairo" && a.Kind == MapPackArtifactKind.Places));
        var gizaRow = Assert.Single(_db.MapPackArtifacts.Where(a => a.GovernorateId == "giza" && a.Kind == MapPackArtifactKind.Places));
        Assert.Equal(1, gizaRow.PlaceCount);
    }

    private const string Digest = "0372f6996c9435ff7e98d774aa11bb22cc33dd44ee55ff66007788990011aabb";

    private sealed class FakeObjectStore(
        IEnumerable<R2Object>? objects = null, IEnumerable<string>? throwOnPutKeys = null) : IMapPackObjectStore
    {
        private readonly List<R2Object> _objects = objects?.ToList() ?? [];
        private readonly HashSet<string> _throwOnPutKeys = throwOnPutKeys?.ToHashSet() ?? [];

        public List<(string Key, byte[] Content, string ContentType, string Sha256)> Puts { get; } = [];

        public Task<IReadOnlyList<R2Object>> ListAsync(string prefix, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<R2Object>>(_objects.Where(o => o.Key.StartsWith(prefix)).ToList());

        public Task PutAsync(string key, byte[] content, string contentType, string sha256, CancellationToken ct)
        {
            if (_throwOnPutKeys.Contains(key))
                throw new InvalidOperationException($"simulated upload failure for {key}");

            Puts.Add((key, content, contentType, sha256));
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
