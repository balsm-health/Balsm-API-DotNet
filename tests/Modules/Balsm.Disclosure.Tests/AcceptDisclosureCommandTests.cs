using Balsm.Disclosure.Application.Commands;
using FluentAssertions;
using Xunit;

namespace Balsm.Disclosure.Tests;

public sealed class AcceptDisclosureCommandTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        var first = new AcceptDisclosureCommand(UserId, "privacy-notice", "2026-01", "EG", "Test Authority", "en");
        var second = new AcceptDisclosureCommand(UserId, "privacy-notice", "2026-01", "EG", "Test Authority", "en");

        var equal = first.Equals(second);

        equal.Should().BeTrue();
    }

    [Fact]
    public void With_ChangedVersion_ProducesDistinctCommand()
    {
        var original = new AcceptDisclosureCommand(UserId, "privacy-notice", "2026-01", "EG", "Test Authority", "en");

        var changed = original with { Version = "2026-02" };

        changed.Should().NotBe(original);
    }

    [Fact]
    public void AcceptDisclosureResult_Constructor_ExposesValues()
    {
        var acceptanceId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

        var result = new AcceptDisclosureResult(acceptanceId, WasAlreadyAccepted: true);

        result.Should().BeEquivalentTo(new { AcceptanceId = acceptanceId, WasAlreadyAccepted = true });
    }
}
