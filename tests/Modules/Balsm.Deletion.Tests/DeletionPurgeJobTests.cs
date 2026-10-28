using System.Reflection;
using Balsm.Account.Domain.Entities;
using Balsm.Account.Infrastructure.Data;
using Balsm.CareTeam.Domain.Entities;
using Balsm.CareTeam.Infrastructure.Data;
using Balsm.Deletion.Domain.Entities;
using Balsm.Deletion.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Jobs;
using Balsm.SharedKernel.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Balsm.Deletion.Tests;

/// <summary>
/// FR-032: the nightly purge. The run itself is a private step behind a cron
/// wait, so it is invoked directly here rather than waiting for 04:00 UTC.
/// </summary>
public sealed class DeletionPurgeJobTests : IDisposable
{
    private readonly SqliteConnection _accountConnection = Open();
    private readonly SqliteConnection _deletionConnection = Open();
    private readonly SqliteConnection _careTeamConnection = Open();
    private readonly ServiceProvider _services;

    public DeletionPurgeJobTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IDomainEventDispatcher>());
        services.AddDbContext<AccountDbContext>(o => o.UseSqlite(_accountConnection));
        services.AddDbContext<DeletionDbContext>(o => o.UseSqlite(_deletionConnection));
        services.AddDbContext<CareTeamDbContext>(o => o.UseSqlite(_careTeamConnection));
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AccountDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<DeletionDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<CareTeamDbContext>().Database.EnsureCreated();
    }

    [Fact]
    public async Task Purge_AccountPastGrace_HardDeletesItAndReleasesItsHandle()
    {
        var expired = await AddAccountAsync(graceUntil: DateTime.UtcNow.AddDays(-1), handle: "expired_handle");

        await PurgeAsync();

        using var scope = _services.CreateScope();
        var accountDb = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        (await accountDb.UserAccounts.AnyAsync(a => a.Id == expired)).Should().BeFalse();
        (await accountDb.UsernameReservations.SingleAsync(r => r.UserId == expired)).ReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Purge_AccountStillInGrace_KeepsIt()
    {
        var pending = await AddAccountAsync(graceUntil: DateTime.UtcNow.AddDays(3), handle: null);

        await PurgeAsync();

        using var scope = _services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AccountDbContext>().UserAccounts.AnyAsync(a => a.Id == pending))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Purge_AccountPastGrace_RemovesItsCareTeamRows()
    {
        var expired = await AddAccountAsync(graceUntil: DateTime.UtcNow.AddDays(-1), handle: null);
        using (var arrange = _services.CreateScope())
        {
            var careTeamDb = arrange.ServiceProvider.GetRequiredService<CareTeamDbContext>();
            careTeamDb.CareProviders.Add(CareProvider.Create(
                Guid.NewGuid(), expired, Guid.NewGuid(), "doctor",
                new CareProviderFields([1], null, null, null, null, null, null, null, null), DateTime.UtcNow));
            await careTeamDb.SaveChangesAsync();
        }

        await PurgeAsync();

        using var scope = _services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<CareTeamDbContext>().CareProviders
            .IgnoreQueryFilters().CountAsync(p => p.UserId == expired)).Should().Be(0);
    }

    [Fact]
    public async Task Purge_DeletionLogPastRetention_RemovesOnlyThatLog()
    {
        await AddAccountAsync(graceUntil: DateTime.UtcNow.AddDays(-1), handle: null);
        using (var arrange = _services.CreateScope())
        {
            var deletionDb = arrange.ServiceProvider.GetRequiredService<DeletionDbContext>();
            var old = DeletionLog.Create("OLDHASH", "EG");
            var recent = DeletionLog.Create("RECENTHASH", "EG");
            deletionDb.DeletionLogs.AddRange(old, recent);
            deletionDb.Entry(old).Property(l => l.PurgeAt).CurrentValue = DateTime.UtcNow.AddDays(-1);
            await deletionDb.SaveChangesAsync();
        }

        await PurgeAsync();

        using var scope = _services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<DeletionDbContext>().DeletionLogs.Select(l => l.UserIdHash).ToListAsync())
            .Should().Equal("RECENTHASH");
    }

    [Fact]
    public async Task StopAsync_WhileWaitingForTheNextRun_EndsTheLoopWithoutPurging()
    {
        var pastGrace = await AddAccountAsync(graceUntil: DateTime.UtcNow.AddDays(-1), handle: null);
        var logger = new FirstLogSignal<DeletionPurgeJob>();
        using var job = new DeletionPurgeJob(_services.GetRequiredService<IServiceScopeFactory>(), logger);
        await job.StartAsync(CancellationToken.None);
        await logger.Logged.WaitAsync(TimeSpan.FromSeconds(10));

        await job.StopAsync(CancellationToken.None);

        job.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
        using var scope = _services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AccountDbContext>().UserAccounts.AnyAsync(a => a.Id == pastGrace))
            .Should().BeTrue();
    }

    public void Dispose()
    {
        _services.Dispose();
        _accountConnection.Dispose();
        _deletionConnection.Dispose();
        _careTeamConnection.Dispose();
    }

    private static SqliteConnection Open()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private async Task<Guid> AddAccountAsync(DateTime graceUntil, string? handle)
    {
        using var scope = _services.CreateScope();
        var accountDb = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
        var account = UserAccount.Create("EG", "en");
        account.RequestDeletion(graceUntil);
        accountDb.UserAccounts.Add(account);
        if (handle is not null) accountDb.UsernameReservations.Add(UsernameReservation.Create(handle, account.Id));
        await accountDb.SaveChangesAsync();
        return account.Id;
    }

    private Task PurgeAsync()
    {
        var job = new DeletionPurgeJob(_services.GetRequiredService<IServiceScopeFactory>(), NullLogger<DeletionPurgeJob>.Instance);
        var purge = typeof(DeletionPurgeJob).GetMethod("PurgeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)purge.Invoke(job, [CancellationToken.None])!;
    }

    /// <summary>Completes once the job logs its first line — the "next run at" line it writes just before waiting.</summary>
    private sealed class FirstLogSignal<T> : ILogger<T>
    {
        private readonly TaskCompletionSource _logged = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Logged => _logged.Task;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _logged.TrySetResult();
    }
}
