using Balsm.Identity.Application.FamilyLinks;
using Balsm.Identity.Domain.FamilyLinks;
using FluentAssertions;
using Xunit;

namespace Balsm.Identity.Tests;

public sealed class FamilyLinkWireTests
{
    [Theory]
    [InlineData(FamilyLinkStatus.Pending, "pending")]
    [InlineData(FamilyLinkStatus.Linked, "linked")]
    [InlineData(FamilyLinkStatus.Declined, "declined")]
    [InlineData(FamilyLinkStatus.Cancelled, "cancelled")]
    [InlineData(FamilyLinkStatus.Unlinked, "unlinked")]
    public void ToWireAndBack_RoundTripsEveryStatus(FamilyLinkStatus status, string wire)
    {
        var parsed = FamilyLinkWire.TryParseStatus(FamilyLinkWire.ToWire(status), out var back);

        (FamilyLinkWire.ToWire(status), parsed, back).Should().Be((wire, true, status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Linked")]
    [InlineData("gone")]
    public void TryParseStatus_RejectsAnythingButTheWireSpelling(string? value)
    {
        FamilyLinkWire.TryParseStatus(value, out _).Should().BeFalse();
    }
}
