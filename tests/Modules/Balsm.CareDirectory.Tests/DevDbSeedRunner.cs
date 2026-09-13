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

public sealed class DevDbSeedRunner
{
    private const string DevConnectionString = "Host=localhost;Port=5433;Database=balsm_dev;Username=postgres;Password=postgres";

    [Fact]
    [Trait("Category", "DevSeed")]
    public async Task SeedPostgresDevDatabase()
    {
        var env = Substitute.For<IHostEnvironment>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Balsm.API.slnx")))
        {
            dir = dir.Parent;
        }
        var contentRoot = dir?.FullName ?? AppContext.BaseDirectory;
        env.ContentRootPath.Returns(contentRoot);

        var opts = new DbContextOptionsBuilder<CareDirectoryDbContext>()
            .UseNpgsql(DevConnectionString)
            .Options;

        await using var probeDb = new CareDirectoryDbContext(opts, Substitute.For<IDomainEventDispatcher>());
        if (!await probeDb.Database.CanConnectAsync())
        {
            return;
        }

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
