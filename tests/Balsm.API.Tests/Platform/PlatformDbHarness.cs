using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Balsm.API.Tests.Platform;

/// <summary>
/// A <see cref="PlatformDbContext"/> over an in-memory SQLite database. One open
/// connection keeps the database alive for the harness; <see cref="NewContext"/>
/// opens another context over it so a test can read back what a write persisted
/// without going through the writer's change tracker.
/// </summary>
internal sealed class PlatformDbHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<PlatformDbContext> _options;

    public PlatformDbHarness(params IInterceptor[] interceptors)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var builder = new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(_connection);
        if (interceptors.Length > 0) builder.AddInterceptors(interceptors);
        _options = builder.Options;

        Db = NewContext();
        Db.Database.EnsureCreated();
    }

    public PlatformDbContext Db { get; }

    public PlatformDbContext NewContext() => new(_options, NullDispatcher.Instance);

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    private sealed class NullDispatcher : IDomainEventDispatcher
    {
        public static readonly NullDispatcher Instance = new();

        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
