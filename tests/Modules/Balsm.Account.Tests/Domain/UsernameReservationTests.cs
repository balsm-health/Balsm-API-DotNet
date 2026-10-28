using Balsm.Account.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Balsm.Account.Tests.Domain;

public sealed class UsernameReservationTests
{
    private static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void Create_MixedCaseHandle_StoresLowercaseForUser()
    {
        var reservation = UsernameReservation.Create("Jane_Doe", UserId);

        reservation.Should().BeEquivalentTo(new { HandleNormalized = "jane_doe", UserId });
    }

    [Fact]
    public void Create_NewReservation_IsActive()
    {
        var reservation = UsernameReservation.Create("jane_doe", UserId);

        reservation.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Release_ActiveReservation_SetsReleasedAt()
    {
        var reservation = UsernameReservation.Create("jane_doe", UserId);

        reservation.Release();

        reservation.ReleasedAt.Should().NotBeNull();
    }

    [Fact]
    public void Release_ActiveReservation_IsNoLongerActive()
    {
        var reservation = UsernameReservation.Create("jane_doe", UserId);

        reservation.Release();

        reservation.IsActive.Should().BeFalse();
    }
}
