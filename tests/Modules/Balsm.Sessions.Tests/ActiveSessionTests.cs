using Balsm.Sessions.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Balsm.Sessions.Tests;

public sealed class ActiveSessionTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DeviceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RefreshTokenId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static ActiveSession CreateSession() =>
        ActiveSession.Create(UserId, DeviceId, "Test Phone", "phone", RefreshTokenId);

    [Fact]
    public void Create_WithValidArguments_MapsAllFields()
    {
        var session = CreateSession();

        session.Should().BeEquivalentTo(new
        {
            UserId,
            DeviceId,
            DeviceLabel = "Test Phone",
            DeviceType = "phone",
            RefreshTokenId,
            RevokedAt = (DateTime?)null,
        });
    }

    [Fact]
    public void Create_Always_AssignsNonEmptyId()
    {
        var session = CreateSession();

        session.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void IsActive_WhenNotRevoked_ReturnsTrue()
    {
        var session = CreateSession();

        var isActive = session.IsActive;

        isActive.Should().BeTrue();
    }

    [Fact]
    public void Revoke_WhenActive_SetsRevokedAtToCurrentUtcTime()
    {
        var session = CreateSession();
        var before = DateTime.UtcNow;

        session.Revoke();

        session.RevokedAt.Should().NotBeNull().And.BeOnOrAfter(before);
    }

    [Fact]
    public void Revoke_WhenActive_MakesSessionInactive()
    {
        var session = CreateSession();

        session.Revoke();

        session.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Touch_Always_AdvancesLastActivityAt()
    {
        var session = CreateSession();
        var before = DateTime.UtcNow;

        session.Touch();

        session.LastActivityAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Touch_Always_LeavesFirstSeenAtUnchanged()
    {
        var session = CreateSession();
        var firstSeenAt = session.FirstSeenAt;

        session.Touch();

        session.FirstSeenAt.Should().Be(firstSeenAt);
    }
}
