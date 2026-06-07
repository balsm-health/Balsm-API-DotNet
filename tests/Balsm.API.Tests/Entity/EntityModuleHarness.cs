using Balsm.Entity.Infrastructure;
using Balsm.Entity.Infrastructure.Data;
using Balsm.Entity.Infrastructure.Repositories;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Balsm.API.Tests.Entity;

/// <summary>
/// In-memory SQLite harness for Entity-module handler integration tests.
/// Exercises the real handler → repository → unit-of-work → DbContext → persistence
/// path. A single open connection keeps the in-memory database alive for the
/// lifetime of the harness; <see cref="NewContext"/> opens a fresh tracking
/// context over the same database so persistence can be verified independently of
/// the write context's change tracker.
/// </summary>
internal sealed class EntityModuleHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<EntityDbContext> _options;

    public EntityDbContext Db { get; }
    public WorkspaceRepository WorkspaceRepo { get; }
    public EntityRepository EntityRepo { get; }
    public BranchRepository BranchRepo { get; }
    public EntityTypeRepository TypeRepo { get; }
    public EntityUnitOfWork Uow { get; }

    public EntityModuleHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<EntityDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new EntityDbContext(_options, NullDispatcher.Instance);
        Db.Database.EnsureCreated(); // applies schema + HasData seed (pharmacy/clinic/hospital)

        WorkspaceRepo = new WorkspaceRepository(Db);
        EntityRepo = new EntityRepository(Db);
        BranchRepo = new BranchRepository(Db);
        TypeRepo = new EntityTypeRepository(Db);
        Uow = new EntityUnitOfWork(Db);
    }

    /// <summary>Fresh context over the same in-memory database for verification reads.</summary>
    public EntityDbContext NewContext() => new(_options, NullDispatcher.Instance);

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
