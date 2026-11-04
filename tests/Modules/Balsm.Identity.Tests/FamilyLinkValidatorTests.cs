using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Application.FamilyLinks.Queries;
using Balsm.Identity.Application.FamilyLinks.Validators;
using FluentAssertions;
using Xunit;
using static Balsm.Identity.Tests.FamilyLinkFixtures;

namespace Balsm.Identity.Tests;

public sealed class FamilyLinkValidatorTests
{
    [Fact]
    public void CreateValidator_ValidCommand_Passes()
    {
        var result = new CreateFamilyLinkCommandValidator().Validate(
            new CreateFamilyLinkCommand(LinkId, Requester, Jti, "spouse", Key(), null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateValidator_EverythingWrong_ReportsEveryFieldAtOnce()
    {
        var result = new CreateFamilyLinkCommandValidator().Validate(
            new CreateFamilyLinkCommand(Guid.Empty, Requester, Guid.Empty, "cousin", new byte[3], null));

        result.Errors.Select(e => (e.PropertyName, e.ErrorCode)).Should().BeEquivalentTo(new[]
        {
            ("Id", "FamilyLink.InvalidId"),
            ("Jti", "FamilyLink.InvalidJti"),
            ("Relation", "FamilyLink.InvalidRelation"),
            ("PublicKey", "FamilyLink.InvalidPublicKey"),
        });
    }

    [Fact]
    public void CreateValidator_WithoutKey_Passes()
    {
        var result = new CreateFamilyLinkCommandValidator().Validate(
            new CreateFamilyLinkCommand(LinkId, Requester, Jti, "son", null, null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(32, true)]
    [InlineData(16, false)]
    public void ApproveValidator_KeyIsOptionalButMustBe32Bytes(int? keyLength, bool valid)
    {
        var result = new ApproveFamilyLinkCommandValidator().Validate(new ApproveFamilyLinkCommand(
            LinkId, Recipient, keyLength is null ? null : new byte[keyLength.Value], null));

        result.IsValid.Should().Be(valid);
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(0, false)]
    public void KeyValidator_RequiresA32ByteKey(int keyLength, bool valid)
    {
        var result = new UpdateFamilyLinkKeyCommandValidator().Validate(
            new UpdateFamilyLinkKeyCommand(LinkId, Requester, new byte[keyLength], null));

        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public void CardValidator_WellFormedEnvelope_Passes()
    {
        var result = new UpsertFamilyLinkCardCommandValidator().Validate(
            new UpsertFamilyLinkCardCommand(LinkId, Requester, 1, Key(), Nonce(), Ciphertext(), null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CardValidator_MalformedEnvelope_ReportsEveryField()
    {
        var result = new UpsertFamilyLinkCardCommandValidator().Validate(
            new UpsertFamilyLinkCardCommand(LinkId, Requester, 0, new byte[1], new byte[1], [], null));

        result.Errors.Select(e => e.PropertyName).Should()
            .BeEquivalentTo("Version", "EphemeralPublicKey", "Nonce", "Ciphertext");
    }

    [Fact]
    public void CardValidator_NullParts_AreRejectedNotThrown()
    {
        var result = new UpsertFamilyLinkCardCommandValidator().Validate(
            new UpsertFamilyLinkCardCommand(LinkId, Requester, 1, null!, null!, null!, null));

        result.Errors.Should().HaveCount(3);
    }

    [Fact]
    public void CardValidator_DoesNotTurnTheSizeCeilingIntoA422()
    {
        var result = new UpsertFamilyLinkCardCommandValidator().Validate(
            new UpsertFamilyLinkCardCommand(LinkId, Requester, 1, Key(), Nonce(), new byte[20_000], null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, 1, 20, true)]
    [InlineData("pending", 1, 100, true)]
    [InlineData("Pending", 1, 20, false)]
    [InlineData("bogus", 1, 20, false)]
    [InlineData(null, 0, 20, false)]
    [InlineData(null, 1, 0, false)]
    [InlineData(null, 1, 101, false)]
    public void ListValidators_CheckStatusAndPaging(string? status, int page, int pageSize, bool valid)
    {
        var incoming = new ListIncomingFamilyLinksQueryValidator().Validate(
            new ListIncomingFamilyLinksQuery(Recipient, status, page, pageSize));
        var outgoing = new ListOutgoingFamilyLinksQueryValidator().Validate(
            new ListOutgoingFamilyLinksQuery(Requester, status, page, pageSize));

        (incoming.IsValid, outgoing.IsValid).Should().Be((valid, valid));
    }
}
