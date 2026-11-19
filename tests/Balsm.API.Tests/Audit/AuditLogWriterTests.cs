using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Platform;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.API.Tests.Audit;

public sealed class AuditLogWriterTests : IDisposable
{
    private static readonly DateTime At = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly PlatformDbContext _db;

    public AuditLogWriterTests()
    {
        _connection.Open();
        _db = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(_connection).Options,
            new NoopDispatcher());
        _db.Database.Migrate();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task WriteAsync_OnSqlite_AssignsIncreasingSequence()
    {
        var writer = new AuditLogWriter(_db);

        await writer.WriteAsync(new AuditLog { OccurredAt = At, Module = "Test", Action = "First" });
        await writer.WriteAsync(new AuditLog { OccurredAt = At, Module = "Test", Action = "Second" });

        _db.AuditLogs.OrderBy(a => a.Sequence).Select(a => a.Sequence).ToList().Should().Equal(1L, 2L);
    }

    private sealed class NoopDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
