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

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
