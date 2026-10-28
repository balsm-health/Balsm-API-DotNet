using Balsm.Disclosure.Infrastructure.Data;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Disclosure.Tests;

/// <summary>
/// One open SQLite in-memory connection for the lifetime of a test; the database
/// is destroyed when the last connection to it closes.
/// </summary>
internal sealed class DisclosureTestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public DisclosureTestDb()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public DisclosureDbContext CreateContext() => new(
        new DbContextOptionsBuilder<DisclosureDbContext>().UseSqlite(_connection).Options,
        new NullDomainEventDispatcher());

    public void Dispose() => _connection.Dispose();
}

internal sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
