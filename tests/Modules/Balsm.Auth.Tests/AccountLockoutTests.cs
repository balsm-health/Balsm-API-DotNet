using Balsm.Auth.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// The rolling-window reset. The window start has no public setter and the
/// entity reads the wall clock, so the start is moved to a fixed past date
/// through the database and the lockout reloaded — the same way a stale row
/// reaches the entity in production.
/// </summary>
public sealed class AccountLockoutTests : IDisposable
{
    private static readonly DateTime LongPast = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string Email = "lockout@example.test";

    private readonly AuthTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private async Task<AccountLockout> LockoutWithStaleWindowAsync(int failures)
    {
        var lockout = AccountLockout.Create(Email, "email");
        for (var i = 0; i < failures; i++) lockout.RecordFailure();
        _database.Db.AccountLockouts.Add(lockout);
        await _database.Db.SaveChangesAsync();
        await _database.Db.AccountLockouts
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.RollingWindowStartedAt, LongPast));
        return await _database.Fresh().AccountLockouts.SingleAsync();
    }

    [Fact]
    public async Task RecordFailure_WindowOlderThanTenMinutes_RestartsCountAtOne()
    {
        var lockout = await LockoutWithStaleWindowAsync(failures: 4);

        lockout.RecordFailure();

        lockout.FailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task RecordFailure_WindowOlderThanTenMinutes_StartsNewWindow()
    {
        var lockout = await LockoutWithStaleWindowAsync(failures: 4);

        lockout.RecordFailure();

        lockout.RollingWindowStartedAt.Should().BeAfter(LongPast);
    }

    [Fact]
    public async Task RecordFailure_FifthFailureAfterStaleWindow_DoesNotLock()
    {
        var lockout = await LockoutWithStaleWindowAsync(failures: 4);

        lockout.RecordFailure();

        lockout.IsLocked.Should().BeFalse();
    }
}
