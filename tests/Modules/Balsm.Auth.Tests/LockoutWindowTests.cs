using Balsm.Auth.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Balsm.Auth.Tests;

/// <summary>
/// T167a: Rolling window + 15-min lock + reset-on-success. FR-007/SC-011.
/// </summary>
public sealed class LockoutWindowTests
{
    [Fact]
    public void FiveFailures_TriggersLock()
    {
        var lockout = AccountLockout.Create("test@example.com", "email");
        for (var i = 0; i < 5; i++)
            lockout.RecordFailure();

        lockout.IsLocked.Should().BeTrue("5 failures should lock the account");
        lockout.LockedUntil.Should().NotBeNull();
        lockout.LockedUntil!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void FourFailures_DoesNotLock()
    {
        var lockout = AccountLockout.Create("test@example.com", "email");
        for (var i = 0; i < 4; i++)
            lockout.RecordFailure();

        lockout.IsLocked.Should().BeFalse("4 failures should not lock");
        lockout.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void RecordSuccess_ResetsLock()
    {
        var lockout = AccountLockout.Create("test@example.com", "email");
        for (var i = 0; i < 5; i++)
            lockout.RecordFailure();

        lockout.IsLocked.Should().BeTrue();

        lockout.RecordSuccess();

        lockout.IsLocked.Should().BeFalse("success should clear lock");
        lockout.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void RecordSuccess_ResetsFailureCount()
    {
        var lockout = AccountLockout.Create("test@example.com", "email");
        for (var i = 0; i < 3; i++)
            lockout.RecordFailure();

        lockout.RecordSuccess();

        // After success, should need 5 more failures to lock
        for (var i = 0; i < 4; i++)
            lockout.RecordFailure();

        lockout.IsLocked.Should().BeFalse("count reset by success; 4 new failures should not lock");
    }

    [Fact]
    public void SixthFailure_StillLocked()
    {
        var lockout = AccountLockout.Create("test@example.com", "email");
        for (var i = 0; i < 6; i++)
            lockout.RecordFailure();

        lockout.IsLocked.Should().BeTrue("locked after 5; 6th failure keeps it locked");
    }

    [Fact]
    public void Create_InitiallyUnlocked()
    {
        var lockout = AccountLockout.Create("user@example.com", "email");
        lockout.IsLocked.Should().BeFalse();
        lockout.LockedUntil.Should().BeNull();
    }

    [Fact]
    public void Create_IdentifierNormalized()
    {
        var lockout = AccountLockout.Create("USER@EXAMPLE.COM", "email");
        lockout.Identifier.Should().Be("user@example.com");
    }
}
