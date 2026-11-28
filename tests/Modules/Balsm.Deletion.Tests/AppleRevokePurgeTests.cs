using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.CareTeam.Infrastructure.Data;
using Balsm.Deletion.Domain.Entities;
using Balsm.Deletion.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Jobs;
using Balsm.SharedKernel.Contracts;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Balsm.Deletion.Tests;

/// The purge revokes each account's Apple grant through the SharedKernel
/// contract and records the outcome on that account's deletion log.
public sealed class AppleRevokePurgeTests : IDisposable
{
    private readonly SqliteConnection _accountConn = Open();
    private readonly SqliteConnection _deletionConn = Open();
    private readonly SqliteConnection _careConn = Open();
    private readonly ServiceProvider _sp;
    private readonly FakeRevoker _revoker = new();
    private FakeAuthEraser? _authEraser;

    public AppleRevokePurgeTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDomainEventDispatcher>());
        services.AddDbContext<AccountDbContext>(o => o.UseSqlite(_accountConn));
        services.AddDbContext<DeletionDbContext>(o => o.UseSqlite(_deletionConn));
        services.AddDbContext<CareTeamDbContext>(o => o.UseSqlite(_careConn));
        services.AddSingleton<IAppleGrantRevoker>(_revoker);
        _authEraser = new FakeAuthEraser(() => StatusOfAsync);
        services.AddSingleton<IAccountAuthEraser>(_authEraser);
        _sp = services.BuildServiceProvider();
        using var scope = _sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<AccountDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<DeletionDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<CareTeamDbContext>().Database.EnsureCreated();
    }

    private static SqliteConnection Open()
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        return c;
    }

    public void Dispose()
    {
        _sp.Dispose();
        _accountConn.Dispose();
        _deletionConn.Dispose();
        _careConn.Dispose();
    }

    private async Task<Guid> SeedExpiredAccountAsync()
    {
        using var scope = _sp.CreateScope();
        var accountDb = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var deletionDb = scope.ServiceProvider.GetRequiredService<DeletionDbContext>();
        var account = UserAccount.Create("EG", "en");
        account.RequestDeletion(DateTime.UtcNow.AddDays(-1));
        accountDb.UserAccounts.Add(account);
        deletionDb.DeletionLogs.Add(DeletionLog.Create(DeletionPurgeJob.ComputeHash(account.Id), "EG"));
        await accountDb.SaveChangesAsync();
        await deletionDb.SaveChangesAsync();
        return account.Id;
    }

    private DeletionPurgeJob Job() =>
        new(_sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<DeletionPurgeJob>.Instance);

    private async Task<string?> StatusOfAsync(Guid userId)
    {
        using var scope = _sp.CreateScope();
        var hash = DeletionPurgeJob.ComputeHash(userId);
        return (await scope.ServiceProvider.GetRequiredService<DeletionDbContext>()
            .DeletionLogs.AsNoTracking().SingleAsync(l => l.UserIdHash == hash)).AppleRevokeStatus;
    }

    [Fact]
    public async Task Purge_RemovesAuthRowsEvenWhenRevokeFails()
    {
        var userId = await SeedExpiredAccountAsync();
        _revoker.Results[userId] = "failed";

        await Job().PurgeAsync(CancellationToken.None);

        _authEraser!.Erased.Should().Equal(userId);
        // The status was already persisted when the auth rows were erased.
        _authEraser.StatusSeenAtErase[userId].Should().Be("failed_final");
    }

    [Fact]
    public async Task Purge_AuthEraserUnavailable_StillPurgesTheAccount()
    {
        var userId = await SeedExpiredAccountAsync();
        _authEraser!.Throws = true;

        await Job().PurgeAsync(CancellationToken.None);

        using var scope = _sp.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AccountDbContext>().UserAccounts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Purge_RecordsAppleRevokeStatusOnDeletionLog()
    {
        var userId = await SeedExpiredAccountAsync();
        _revoker.Results[userId] = "revoked";

        await Job().PurgeAsync(CancellationToken.None);

        (await StatusOfAsync(userId)).Should().Be("succeeded");
        _revoker.Calls.Should().Equal(userId);
    }

    [Fact]
    public async Task Purge_RevokerFailsForOneAccount_StillPurgesTheRest()
    {
        var first = await SeedExpiredAccountAsync();
        var second = await SeedExpiredAccountAsync();
        _revoker.Results[first] = "failed";
        _revoker.Results[second] = "revoked";

        await Job().PurgeAsync(CancellationToken.None);

        using var scope = _sp.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AccountDbContext>().UserAccounts.CountAsync()).Should().Be(0);
        (await StatusOfAsync(first)).Should().Be("failed_final");
        (await StatusOfAsync(second)).Should().Be("succeeded");
    }

    [Fact]
    public async Task Purge_RevokerThrows_StillPurgesTheAccount()
    {
        var userId = await SeedExpiredAccountAsync();
        _revoker.Throwing.Add(userId);

        await Job().PurgeAsync(CancellationToken.None);

        using var scope = _sp.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AccountDbContext>().UserAccounts.CountAsync()).Should().Be(0);
        (await StatusOfAsync(userId)).Should().Be("failed_final");
    }

    private sealed class FakeAuthEraser(Func<Func<Guid, Task<string?>>> status) : IAccountAuthEraser
    {
        public List<Guid> Erased { get; } = [];
        public Dictionary<Guid, string?> StatusSeenAtErase { get; } = [];
        public bool Throws { get; set; }

        public async Task<int> EraseForAccountAsync(Guid userId, CancellationToken ct = default)
        {
            if (Throws) throw new InvalidOperationException("boom");
            Erased.Add(userId);
            StatusSeenAtErase[userId] = await status()(userId);
            return 1;
        }
    }

    private sealed class FakeRevoker : IAppleGrantRevoker
    {
        public Dictionary<Guid, string> Results { get; } = [];
        public HashSet<Guid> Throwing { get; } = [];
        public List<Guid> Calls { get; } = [];

        public Task<string> RevokeForAccountAsync(Guid userId, CancellationToken ct = default)
        {
            Calls.Add(userId);
            if (Throwing.Contains(userId)) throw new InvalidOperationException("boom");
            return Task.FromResult(Results.GetValueOrDefault(userId, "no_apple_identity"));
        }
    }
}
