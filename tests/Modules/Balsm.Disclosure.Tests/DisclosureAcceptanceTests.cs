using Balsm.Disclosure.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Balsm.Disclosure.Tests;

public sealed class DisclosureAcceptanceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DisclosureAcceptance CreateAcceptance() =>
        DisclosureAcceptance.Create(UserId, "privacy-notice", "2026-01", "EG", "Test Authority", "ar-EG");

    [Fact]
    public void Create_WithValidArguments_MapsAllFields()
    {
        var acceptance = CreateAcceptance();

        acceptance.Should().BeEquivalentTo(new
        {
            UserId,
            DisclosureId = "privacy-notice",
            Version = "2026-01",
            CountryCodeAtAccept = "EG",
            SupervisoryAuthorityNameAtAccept = "Test Authority",
            PreferredLanguageAtAccept = "ar-EG",
        });
    }

    [Fact]
    public void Create_Always_AssignsNonEmptyId()
    {
        var acceptance = CreateAcceptance();

        acceptance.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_Always_StampsAcceptedAtWithCurrentTime()
    {
        var before = DateTime.UtcNow;

        var acceptance = CreateAcceptance();

        acceptance.AcceptedAt.Should().BeOnOrAfter(before);
    }
}
