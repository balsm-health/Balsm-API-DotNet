using Balsm.Geofence.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Geofence.Tests;

/// <summary>
/// One open SQLite in-memory connection for the lifetime of a test; EnsureCreated
/// applies the model's HasData seed (the OFAC blocklist: CU, IR, KP, SY).
/// </summary>
internal sealed class GeofenceTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public GeofenceTestDb()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public GeofenceDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GeofenceDbContext>().UseSqlite(_connection).Options,
        new NullDomainEventDispatcher());

    public void Dispose() => _connection.Dispose();
}

internal sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
