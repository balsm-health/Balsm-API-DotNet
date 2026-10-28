using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.Infrastructure.Encryption;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Balsm.Account.Tests;

/// <summary>
/// SQLite in-memory Account database for handler tests. The connection stays
/// open for the fixture lifetime — a :memory: database is destroyed as soon as
/// its last connection closes.
/// </summary>
internal sealed class AccountTestDatabase : IDisposable
{
    public static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid OtherUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid MissingUserId = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    private readonly SqliteConnection _connection;

    public AccountTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Db = NewContext();
        Db.Database.EnsureCreated();
    }

    public AccountDbContext Db { get; }

    /// <summary>A second context on the same database, so assertions read
    /// persisted state rather than the tracked instance.</summary>
    public AccountDbContext NewContext() => new(
        new DbContextOptionsBuilder<AccountDbContext>().UseSqlite(_connection).Options,
        new NullDomainEventDispatcher());

    public static DobEncryptionService CreateEncryption() => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DobEncryption:Key"] = Convert.ToBase64String(new byte[32]),
        }).Build(),
        NullLogger<DobEncryptionService>.Instance);

    public async Task<UserAccount> SeedAccountAsync(Action<UserAccount>? configure = null)
    {
        var account = UserAccount.CreateWithId(UserId, "SA", "en");
        configure?.Invoke(account);
        Db.UserAccounts.Add(account);
        await Db.SaveChangesAsync();
        return account;
    }

    public async Task SeedReservationAsync(string handle, Guid userId)
    {
        Db.UsernameReservations.Add(UsernameReservation.Create(handle, userId));
        await Db.SaveChangesAsync();
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    private sealed class NullDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
