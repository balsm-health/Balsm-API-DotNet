using Xunit;
using Balsm.CareDirectory.Domain.Entities;
using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.MapPacks;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Balsm.CareDirectory.Tests;

public sealed class LocalMapPackSeedServiceTests : IDisposable
{
    private readonly CareDirectoryDbContext _db;
    private readonly SqliteConnection _connection;
    private readonly IHostEnvironment _env;

    public LocalMapPackSeedServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new CareDirectoryDbContext(
            new DbContextOptionsBuilder<CareDirectoryDbContext>().UseSqlite(_connection).Options,
            Substitute.For<IDomainEventDispatcher>());
        _db.Database.EnsureCreated();

        _env = Substitute.For<IHostEnvironment>();
        _env.ContentRootPath.Returns(AppContext.BaseDirectory);
    }

    [Fact]
    public async Task SkipsWhenR2IsConfigured()
    {
        var options = Options.Create(new MapPackR2Options
        {
            AccountId = "acc",
            AccessKeyId = "key",
            SecretAccessKey = "secret",
            Bucket = "bucket",
            CdnBaseUrl = "https://cdn.example.com"
        });

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var service = new LocalMapPackSeedService(
            scopeFactory, options, _env, Substitute.For<ILogger<LocalMapPackSeedService>>());

        await service.StartAsync(default);

        scopeFactory.DidNotReceive().CreateAsyncScope();
    }

    [Fact]
    public async Task SeedsBasemapsAndPlacesWhenPlacesExist()
    {
        // Seed at least one care place in Cairo bbox
        _db.CarePlaces.Add(CarePlace.Create(
            type: "pharmacy",
            nameEn: "Cairo Pharmacy",
            nameAr: "صيدلية القاهرة",
            addressEn: null,
            addressAr: null,
            lat: 30.0444,
            lng: 31.2357,
            hours: null,
            phone: null,
            rating: 4.5,
            countryCode: "EG",
            confidence: 0.9,
            externalId: "ext-1",
            source: "test"));
        await _db.SaveChangesAsync();

        var options = Options.Create(new MapPackR2Options()); // unconfigured

        var opts = new DbContextOptionsBuilder<CareDirectoryDbContext>().UseSqlite(_connection).Options;
        var services = new ServiceCollection();
        services.AddScoped(_ => new CareDirectoryDbContext(opts, Substitute.For<IDomainEventDispatcher>()));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var service = new LocalMapPackSeedService(
            scopeFactory, options, _env, Substitute.For<ILogger<LocalMapPackSeedService>>());

        await service.StartAsync(default);

        // Verify basemaps seeded
        var basemaps = await _db.MapPackArtifacts.Where(a => a.Kind == MapPackArtifactKind.Basemap).ToListAsync();
        Assert.Equal(27, basemaps.Count);

        // Verify places seeded
        var places = await _db.MapPackArtifacts.Where(a => a.Kind == MapPackArtifactKind.Places).ToListAsync();
        Assert.Equal(27, places.Count);

        var cairoPlaces = places.Single(p => p.GovernorateId == "cairo");
        Assert.Equal(1, cairoPlaces.PlaceCount);
        Assert.StartsWith("/care/packs/places/cairo-", cairoPlaces.Url);
    }

    [Fact]
    public async Task StartAsync_NoCarePlaces_SeedsBasemapsButNoPlaces()
    {
        var service = Service(_connection);

        await service.StartAsync(default);

        Assert.Equal(27, await _db.MapPackArtifacts.CountAsync(a => a.Kind == MapPackArtifactKind.Basemap));
        Assert.Equal(0, await _db.MapPackArtifacts.CountAsync(a => a.Kind == MapPackArtifactKind.Places));
    }

    [Fact]
    public async Task StartAsync_StaleBasemapRow_RepublishesItInPlace()
    {
        var stale = MapPackArtifact.Publish(
            "cairo", "Cairo", "القاهرة", MapPackArtifactKind.Basemap, "19990101",
            1, new string('0', 64), "https://cdn.example.test/packs/cairo-19990101.pmtiles", 0, 0, 1, 1);
        _db.MapPackArtifacts.Add(stale);
        await _db.SaveChangesAsync();
        var service = Service(_connection);

        await service.StartAsync(default);

        var cairo = await _db.MapPackArtifacts.AsNoTracking()
            .SingleAsync(a => a.Kind == MapPackArtifactKind.Basemap && a.GovernorateId == "cairo");
        Assert.Equal(stale.Id, cairo.Id);
        Assert.NotEqual("19990101", cairo.Version);
    }

    [Fact]
    public async Task StartAsync_SecondRunAfterANewPlace_UpdatesThePlaceCount()
    {
        AddCairoPlace("ext-run-1");
        await _db.SaveChangesAsync();
        await Service(_connection).StartAsync(default);
        AddCairoPlace("ext-run-2");
        await _db.SaveChangesAsync();

        await Service(_connection).StartAsync(default);

        var cairo = await _db.MapPackArtifacts.AsNoTracking()
            .SingleAsync(a => a.Kind == MapPackArtifactKind.Places && a.GovernorateId == "cairo");
        Assert.Equal(2, cairo.PlaceCount);
    }

    [Fact]
    public async Task StartAsync_DatabaseWithoutSchema_LogsAndDoesNotThrow()
    {
        using var empty = new SqliteConnection("Data Source=:memory:");
        empty.Open();
        var logger = Substitute.For<ILogger<LocalMapPackSeedService>>();
        var service = Service(empty, logger);

        await service.StartAsync(default);

        logger.Received().Log(
            LogLevel.Error, Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception?>(), Arg.Any<Func<object, Exception?, string>>());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private LocalMapPackSeedService Service(SqliteConnection connection, ILogger<LocalMapPackSeedService>? logger = null)
    {
        var opts = new DbContextOptionsBuilder<CareDirectoryDbContext>().UseSqlite(connection).Options;
        var services = new ServiceCollection();
        services.AddScoped(_ => new CareDirectoryDbContext(opts, Substitute.For<IDomainEventDispatcher>()));
        return new LocalMapPackSeedService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MapPackR2Options()),
            _env,
            logger ?? Substitute.For<ILogger<LocalMapPackSeedService>>());
    }

    private void AddCairoPlace(string externalId) =>
        _db.CarePlaces.Add(CarePlace.Create(
            type: "pharmacy",
            nameEn: "Example Pharmacy",
            nameAr: "صيدلية",
            addressEn: null,
            addressAr: null,
            lat: 30.0444,
            lng: 31.2357,
            hours: null,
            phone: null,
            rating: null,
            countryCode: "EG",
            confidence: 0.9,
            externalId: externalId,
            source: "test"));
}
