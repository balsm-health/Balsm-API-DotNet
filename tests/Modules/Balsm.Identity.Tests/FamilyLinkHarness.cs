using System.Data.Common;
using Balsm.Identity.Infrastructure.Data;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Contracts;
using Balsm.SharedKernel.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using static Balsm.Identity.Tests.FamilyLinkFixtures;

namespace Balsm.Identity.Tests;

/// <summary>
/// A real IdentityDbContext over in-memory SQLite plus substitutes for the two
/// cross-module contracts. <see cref="BeforeNextSave"/> runs raw SQL just before
/// the next SaveChanges, to stage the races the handlers must survive.
/// </summary>
internal sealed class FamilyLinkHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly RaceInterceptor _race = new();

    public FamilyLinkHarness()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Db = NewContext();
        Db.Database.EnsureCreated();

        QrOwners.ResolveOwnerAsync(Jti, Arg.Any<CancellationToken>()).Returns(Recipient);
        Names.GetAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyDictionary<Guid, AccountDisplayName>)call.Arg<IReadOnlyCollection<Guid>>()
                .ToDictionary(id => id, id => new AccountDisplayName($"handle{id.ToString()[^1]}", $"Member {id.ToString()[^1]}")));

        Store = new FamilyLinkStore(Db, new FixedClock(Now), NullLogger<FamilyLinkStore>.Instance);
        Dtos = new FamilyLinkDtoFactory(Names);
    }

    public IdentityDbContext Db { get; }
    public IProfileQrOwnerResolver QrOwners { get; } = Substitute.For<IProfileQrOwnerResolver>();
    public IAccountDisplayNameReader Names { get; } = Substitute.For<IAccountDisplayNameReader>();
    public FamilyLinkStore Store { get; }
    public FamilyLinkDtoFactory Dtos { get; }

    /// <summary>A second context on the same database, for reading back what was committed.</summary>
    public IdentityDbContext Fresh() => NewContext();

    public void BeforeNextSave(string sql) => _race.Pending = sql;

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    private IdentityDbContext NewContext() => new(
        new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_connection).AddInterceptors(_race).Options,
        Substitute.For<IDomainEventDispatcher>());

    private sealed class RaceInterceptor : SaveChangesInterceptor
    {
        public string? Pending { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Pending is { } sql)
            {
                Pending = null;
                DbConnection connection = eventData.Context!.Database.GetDbConnection();
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }
}
