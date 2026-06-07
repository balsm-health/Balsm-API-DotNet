using System.Diagnostics;
using Balsm.Entity.Domain;
using Balsm.Entity.Infrastructure.Data;
using Balsm.Infrastructure.Configuration;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.API.Tests.Entity;

/// <summary>
/// Phase 0 exit criterion: "SQLite handles 50 concurrent read connections without
/// degradation." Exercises a real file-backed database configured through the
/// production <see cref="DatabaseServiceExtensions.ConfigureDatabase"/> path, so the
/// WAL journal mode + busy_timeout pragmas from <c>SqlitePragmaInterceptor</c> are
/// applied exactly as they are at runtime.
/// </summary>
public sealed class SqliteConcurrentReadTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"balsm-conc-{Guid.NewGuid():N}.db");
    private const int ConcurrentReaders = 50;
    private const int SeededEntities = 200;

    private DbContextOptions<EntityDbContext> BuildOptions()
    {
        var builder = new DbContextOptionsBuilder<EntityDbContext>();
        builder.ConfigureDatabase(new DatabaseOptions
        {
            Provider = "sqlite",
            ConnectionString = $"Data Source={_dbPath}",
        });
        return (DbContextOptions<EntityDbContext>)builder.Options;
    }

    private EntityDbContext NewContext(DbContextOptions<EntityDbContext> options)
        => new(options, NullDispatcher.Instance);

    [Fact]
    public async Task FiftyConcurrentReaders_AllSucceed_NoBusyErrors()
    {
        var options = BuildOptions();
        var workspaceId = Guid.NewGuid();

        // Seed once.
        await using (var seed = NewContext(options))
        {
            await seed.Database.EnsureCreatedAsync();
            for (var i = 0; i < SeededEntities; i++)
                await seed.Entities.AddAsync(EntityRoot.Create(workspaceId, $"E{i}", "pharmacy"));
            await seed.SaveChangesAsync();
        }

        // Hold 50 connections open simultaneously (genuine concurrent connections,
        // driven by async I/O rather than 50 OS threads), then read on all at once.
        var contexts = Enumerable.Range(0, ConcurrentReaders).Select(_ => NewContext(options)).ToList();
        try
        {
            // Force all 50 connections open at the same time.
            await Task.WhenAll(contexts.Select(c => c.Database.OpenConnectionAsync()));

            var sw = Stopwatch.StartNew();
            var counts = await Task.WhenAll(contexts.Select(c => c.Entities
                .AsNoTracking()
                .Where(e => e.WorkspaceId == workspaceId)
                .CountAsync()));
            sw.Stop();

            counts.Should().OnlyContain(c => c == SeededEntities,
                "every concurrent reader must see the full committed dataset");
            sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
                "50 concurrent WAL readers should complete without busy-timeout contention");
        }
        finally
        {
            foreach (var c in contexts)
            {
                await c.Database.CloseConnectionAsync();
                await c.DisposeAsync();
            }
        }
    }

    public void Dispose()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch (IOException) { /* best-effort temp cleanup */ }
            }
        }
    }

    private sealed class NullDispatcher : IDomainEventDispatcher
    {
        public static readonly NullDispatcher Instance = new();

        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
