using Balsm.CareDirectory.Infrastructure.Data;
using Balsm.CareDirectory.Infrastructure.Import;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Balsm.CareDirectory.Tests;

/// <summary>
/// The startup import: skipped when switched off, harmless when the artifact
/// is missing or the import fails, and a real import of the six-row fixture
/// otherwise.
/// </summary>
public sealed class CareDirectoryImportServiceTests : IDisposable
{
    private const string FixturePath = "Fixtures/care_places.sample.ndjson";
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();

    public CareDirectoryImportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDomainEventDispatcher>());
        services.AddDbContext<CareDirectoryDbContext>(o => o.UseSqlite(_connection));
        _services = services.BuildServiceProvider();
        _environment.ContentRootPath.Returns(Path.Combine(AppContext.BaseDirectory, "no-such-content-root"));
    }

    [Fact]
    public async Task StartAsync_ImportDisabled_ImportsNothing()
    {
        EnsureCreated();
        var service = Service(new CareDirectoryOptions { ImportOnStartup = false, ArtifactPath = FixturePath });

        await service.StartAsync(CancellationToken.None);

        PlaceCount().Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_ArtifactMissing_ImportsNothingAndDoesNotThrow()
    {
        EnsureCreated();
        var service = Service(new CareDirectoryOptions { ArtifactPath = "Fixtures/does-not-exist.ndjson" });

        await service.StartAsync(CancellationToken.None);

        PlaceCount().Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_ArtifactPresent_ImportsTheRowsAboveTheFloor()
    {
        EnsureCreated();
        var service = Service(new CareDirectoryOptions { ArtifactPath = FixturePath });

        await service.StartAsync(CancellationToken.None);

        PlaceCount().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StartAsync_ArtifactOnlyUnderTheContentRoot_IsStillFound()
    {
        EnsureCreated();
        var contentRoot = Directory.CreateTempSubdirectory("care-directory-content-root-");
        var relative = "artifact.ndjson";
        File.Copy(Path.Combine(AppContext.BaseDirectory, FixturePath), Path.Combine(contentRoot.FullName, relative));
        _environment.ContentRootPath.Returns(contentRoot.FullName);
        var service = Service(new CareDirectoryOptions { ArtifactPath = relative });

        await service.StartAsync(CancellationToken.None);

        PlaceCount().Should().BeGreaterThan(0);
        contentRoot.Delete(recursive: true);
    }

    [Fact]
    public async Task StartAsync_ImportFails_DoesNotThrow()
    {
        // No schema: the import's first query fails, which must be logged, not thrown.
        var service = Service(new CareDirectoryOptions { ArtifactPath = FixturePath });

        var act = () => service.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_Always_Completes()
    {
        var service = Service(new CareDirectoryOptions());

        var act = () => service.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    private CareDirectoryImportService Service(CareDirectoryOptions options) => new(
        _services.GetRequiredService<IServiceScopeFactory>(),
        _environment,
        Options.Create(options),
        NullLogger<CareDirectoryImportService>.Instance);

    private void EnsureCreated()
    {
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CareDirectoryDbContext>().Database.EnsureCreated();
    }

    private int PlaceCount()
    {
        using var scope = _services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CareDirectoryDbContext>().CarePlaces.Count();
    }
}
