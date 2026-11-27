using Balsm.Infrastructure.Lifecycle;
using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Balsm.API.Tests.Lifecycle;

public sealed class MigrationRecoveryServiceTests : IDisposable
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services;

    public MigrationRecoveryServiceTests()
    {
        _connection.Open();
        _services = new ServiceCollection()
            .AddScoped<IDomainEventDispatcher, DomainEventDispatcher>()
            .AddDbContext<PlatformDbContext>(o => o.UseSqlite(_connection))
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RunAsync_FreshDatabase_DoesNothing()
    {
        var act = () => Service().RunAsync(default);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RunAsync_IncompleteRecords_MarksThemAsRecovered()
    {
        await MigrateAsync();
        await SeedAsync(
            new MigrationStateRecord { DbContextName = "A", MigrationId = "1", StartedAt = StartedAt },
            new MigrationStateRecord { DbContextName = "B", MigrationId = "2", StartedAt = StartedAt, CompletedAt = StartedAt, Succeeded = true });

        await Service().RunAsync(default);

        (await ReadAsync()).Select(r => r.ErrorMessage)
            .Should().Equal("Recovered at startup — previous run did not complete", null);
    }

    [Fact]
    public async Task RunAsync_AllRecordsComplete_ChangesNothing()
    {
        await MigrateAsync();
        await SeedAsync(new MigrationStateRecord
        {
            DbContextName = "A",
            MigrationId = "1",
            StartedAt = StartedAt,
            CompletedAt = StartedAt,
            Succeeded = true,
        });

        await Service().RunAsync(default);

        (await ReadAsync()).Single().ErrorMessage.Should().BeNull();
    }

    private MigrationRecoveryService Service() =>
        new(_services.GetRequiredService<IServiceScopeFactory>(), NullLogger<MigrationRecoveryService>.Instance);

    private async Task MigrateAsync()
    {
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
    }

    private async Task SeedAsync(params MigrationStateRecord[] records)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.MigrationStateRecords.AddRange(records);
        await db.SaveChangesAsync();
    }

    private async Task<List<MigrationStateRecord>> ReadAsync()
    {
        using var scope = _services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
            .MigrationStateRecords.AsNoTracking().OrderBy(r => r.DbContextName).ToListAsync();
    }
}
