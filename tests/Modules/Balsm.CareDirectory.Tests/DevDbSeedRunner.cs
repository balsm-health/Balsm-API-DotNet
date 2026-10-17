using Xunit;
using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.MapPacks;
using Balsm.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// Developer convenience: seeds a LOCAL Postgres dev database with map-pack
/// rows by running the real <see cref="LocalMapPackSeedService"/> against it.
///
/// Opt-in only, via the BALSM_DEV_SEED_CONNECTION environment variable:
///
///   BALSM_DEV_SEED_CONNECTION="Host=localhost;Port=5433;Database=balsm_dev;Username=…;Password=…" \
///     dotnet test tests/Modules/Balsm.CareDirectory.Tests --filter "Category=DevSeed"
///
/// Without that variable this is a no-op. The gate is deliberate: this writes
/// to whatever database the connection string names, so it must never run
/// because a developer happened to have Postgres listening on a guessable
/// port. The connection string is never hardcoded here for the same reason
/// no credential is hardcoded anywhere in this repo.
/// </summary>
public sealed class DevDbSeedRunner
{
    private static string? DevConnectionString =>
        Environment.GetEnvironmentVariable("BALSM_DEV_SEED_CONNECTION");

    [Fact]
    [Trait("Category", "DevSeed")]
    public async Task SeedPostgresDevDatabase()
    {
        var connectionString = DevConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Not opted in — this is the normal path for `dotnet test` and CI.
            return;
        }

        var env = Substitute.For<IHostEnvironment>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Balsm.API.slnx")))
        {
            dir = dir.Parent;
        }
        var contentRoot = dir?.FullName ?? AppContext.BaseDirectory;
        env.ContentRootPath.Returns(contentRoot);

        var opts = new DbContextOptionsBuilder<CareDirectoryDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var probeDb = new CareDirectoryDbContext(opts, Substitute.For<IDomainEventDispatcher>());
        // Opted in but unreachable is a real mistake (wrong port, container
        // down), not something to pass silently — the caller asked for a seed
        // and would otherwise see green without one having happened.
        Assert.True(
            await probeDb.Database.CanConnectAsync(),
            "BALSM_DEV_SEED_CONNECTION is set but the database is unreachable.");

        var services = new ServiceCollection();
        services.AddScoped(_ => new CareDirectoryDbContext(opts, Substitute.For<IDomainEventDispatcher>()));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var options = Options.Create(new MapPackR2Options()); // unconfigured, triggers local seed
        var service = new LocalMapPackSeedService(
            scopeFactory, options, env, Substitute.For<ILogger<LocalMapPackSeedService>>());

        await service.StartAsync(default);

        // Verify that map_pack_artifact in Postgres now has 27 basemaps and 27 places
        await using var verifyDb = new CareDirectoryDbContext(opts, Substitute.For<IDomainEventDispatcher>());
        var basemaps = await verifyDb.MapPackArtifacts.Where(a => a.Kind == Domain.Entities.MapPackArtifactKind.Basemap).CountAsync();
        var places = await verifyDb.MapPackArtifacts.Where(a => a.Kind == Domain.Entities.MapPackArtifactKind.Places).CountAsync();

        Assert.Equal(27, basemaps);
        Assert.Equal(27, places);
    }
}
