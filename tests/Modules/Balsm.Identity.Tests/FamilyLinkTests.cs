using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Domain.FamilyLinks.Events;
using FluentAssertions;
using Xunit;
using static Balsm.Identity.Tests.FamilyLinkFixtures;

namespace Balsm.Identity.Tests;

public sealed class FamilyLinkTests
{
    [Fact]
    public void Request_WithValidInput_OpensPendingLinkWithRequesterConsent()
    {
        var result = FamilyLink.Request(LinkId, Requester, Recipient, "child", Key(), Now);

        result.IsSuccess.Should().BeTrue();
        var link = result.Value!;
        link.Status.Should().Be(FamilyLinkStatus.Pending);
        link.RequesterPublicKey.Should().Equal(Key());
        link.CreatedAt.Should().Be(Now);
        link.Consents.Should().ContainSingle(c =>
            c.UserId == Requester && c.Purpose == FamilyLinkConsentPurpose.Link && c.GrantedAt == Now && c.IsActive);
    }

    [Fact]
    public void Request_OrdersThePairKeyIndependentlyOfDirection()
    {
        var forward = FamilyLink.Request(LinkId, Requester, Recipient, "child", null, Now).Value!;
        var backward = FamilyLink.Request(Guid.NewGuid(), Recipient, Requester, "parent", null, Now).Value!;

        (backward.PairLowUserId, backward.PairHighUserId).Should().Be((forward.PairLowUserId, forward.PairHighUserId));
    }

    [Fact]
    public void Request_WithEmptyId_FailsInvalidId()
    {
        var result = FamilyLink.Request(Guid.Empty, Requester, Recipient, "child", null, Now);

        result.Error.Should().Be(FamilyLinkErrors.InvalidId);
    }

    [Fact]
    public void Request_ToSelf_FailsSelfLink()
    {
        var result = FamilyLink.Request(LinkId, Requester, Requester, "child", null, Now);

        result.Error.Should().Be(FamilyLinkErrors.SelfLink);
    }

    [Theory]
    [InlineData("cousin")]
    [InlineData("Child")]
    [InlineData("")]
    public void Request_WithUnknownRelation_FailsInvalidRelation(string relation)
    {
        var result = FamilyLink.Request(LinkId, Requester, Recipient, relation, null, Now);

        result.Error.Should().Be(FamilyLinkErrors.InvalidRelation);
    }

    [Fact]
    public void Request_WithShortPublicKey_FailsInvalidPublicKey()
    {
        var result = FamilyLink.Request(LinkId, Requester, Recipient, "child", new byte[31], Now);

        result.Error.Should().Be(FamilyLinkErrors.InvalidPublicKey);
    }

    [Fact]
    public void Approve_ByRecipient_LinksRecordsConsentAndRaisesEvent()
    {
        var link = Pending();
        var stampBefore = link.ConcurrencyStamp;

        var result = link.Approve(Recipient, Key(3), Now.AddMinutes(1));

        result.IsSuccess.Should().BeTrue();
        link.Status.Should().Be(FamilyLinkStatus.Linked);
        link.LinkedAt.Should().Be(Now.AddMinutes(1));
        link.RespondedAt.Should().Be(Now.AddMinutes(1));
        link.RecipientPublicKey.Should().Equal(Key(3));
        link.ConcurrencyStamp.Should().NotBe(stampBefore);
        link.Consents.Should().Contain(c => c.UserId == Recipient && c.Purpose == FamilyLinkConsentPurpose.Link);
        link.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<FamilyLinkApproved>()
            .Which.Should().Match<FamilyLinkApproved>(e =>
                e.FamilyLinkId == LinkId && e.RequesterUserId == Requester && e.RecipientUserId == Recipient && e.EventId != Guid.Empty);
    }

    [Fact]
    public void Approve_WithoutKey_KeepsRecipientKeyUnset()
    {
        var link = Pending();

        link.Approve(Recipient, null, Now);

        link.RecipientPublicKey.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(NonRecipients))]
    public void Approve_ByAnyoneButRecipient_FailsNotFound(Guid actor)
    {
        var result = Pending().Approve(actor, null, Now);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public void Approve_WhenNotPending_FailsNotPending()
    {
        var result = Linked().Approve(Recipient, null, Now);

        result.Error.Should().Be(FamilyLinkErrors.NotPending);
    }

    [Fact]
    public void Approve_WithBadKey_FailsAndStaysPending()
    {
        var link = Pending();

        var result = link.Approve(Recipient, new byte[5], Now);

        result.Error.Should().Be(FamilyLinkErrors.InvalidPublicKey);
        link.Status.Should().Be(FamilyLinkStatus.Pending);
    }

    [Fact]
    public void Decline_ByRecipient_DeclinesAndWithdrawsConsent()
    {
        var link = Pending();

        var result = link.Decline(Recipient, Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        link.Status.Should().Be(FamilyLinkStatus.Declined);
        link.RespondedAt.Should().Be(Now.AddHours(1));
        link.Consents.Should().OnlyContain(c => c.WithdrawnAt == Now.AddHours(1));
    }

    [Theory]
    [MemberData(nameof(NonRecipients))]
    public void Decline_ByAnyoneButRecipient_FailsNotFound(Guid actor)
    {
        var result = Pending().Decline(actor, Now);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public void Decline_WhenNotPending_FailsNotPending()
    {
        var result = Linked().Decline(Recipient, Now);

        result.Error.Should().Be(FamilyLinkErrors.NotPending);
    }

    [Fact]
    public void End_ByRequesterWhilePending_Cancels()
    {
        var link = Pending();

        var result = link.End(Requester, Now.AddDays(1));

        result.Value.Should().BeTrue();
        link.Status.Should().Be(FamilyLinkStatus.Cancelled);
        link.EndedAt.Should().Be(Now.AddDays(1));
        link.EndedByUserId.Should().Be(Requester);
        link.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void End_ByRecipientWhilePending_FailsRecipientMustDecline()
    {
        var result = Pending().End(Recipient, Now);

        result.Error.Should().Be(FamilyLinkErrors.RecipientMustDecline);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void End_ByEitherPartyWhileLinked_UnlinksAndPurgesEveryCard(bool byRequester)
    {
        var link = Linked();
        link.ShareCard(Requester, 1, Key(), Nonce(), Ciphertext(), Now);
        link.ShareCard(Recipient, 1, Key(), Nonce(), Ciphertext(), Now);

        var result = link.End(byRequester ? Requester : Recipient, Now.AddDays(2));

        result.Value.Should().BeTrue();
        link.Status.Should().Be(FamilyLinkStatus.Unlinked);
        link.CardShares.Should().BeEmpty();
        link.Consents.Should().OnlyContain(c => !c.IsActive);
        link.LinkedAt.Should().NotBeNull();
    }

    [Fact]
    public void End_WhenAlreadyEnded_IsIdempotentNoChange()
    {
        var link = Pending();
        link.End(Requester, Now);
        var stamp = link.ConcurrencyStamp;

        var result = link.End(Requester, Now.AddDays(5));

        result.Value.Should().BeFalse();
        link.EndedAt.Should().Be(Now);
        link.ConcurrencyStamp.Should().Be(stamp);
    }

    [Fact]
    public void End_ByStranger_FailsNotFound()
    {
        var result = Linked().End(Stranger, Now);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetPublicKey_ByParty_StoresTheirKey(bool byRequester)
    {
        var link = Pending();
        var actor = byRequester ? Requester : Recipient;

        var result = link.SetPublicKey(actor, Key(4));

        result.IsSuccess.Should().BeTrue();
        link.PublicKeyOf(actor).Should().Equal(Key(4));
        link.PublicKeyOf(link.CounterpartOf(actor)).Should().BeNull();
    }

    [Fact]
    public void SetPublicKey_ByStranger_FailsNotFound()
    {
        var result = Pending().SetPublicKey(Stranger, Key());

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public void SetPublicKey_OnEndedLink_FailsEnded()
    {
        var link = Pending();
        link.Decline(Recipient, Now);

        var result = link.SetPublicKey(Requester, Key());

        result.Error.Should().Be(FamilyLinkErrors.Ended);
    }

    [Fact]
    public void SetPublicKey_WithBadKey_FailsInvalidPublicKey()
    {
        var result = Pending().SetPublicKey(Requester, new byte[33]);

        result.Error.Should().Be(FamilyLinkErrors.InvalidPublicKey);
    }

    [Fact]
    public void ShareCard_FirstVersion_StoresEnvelopeAndRecordsOptInConsent()
    {
        var link = Linked();

        var result = link.ShareCard(Requester, 1, Key(), Nonce(), Ciphertext(), Now.AddHours(2));

        result.Value.Should().BeTrue();
        var card = link.CardOwnedBy(Requester)!;
        (card.Version, card.UpdatedAt, card.OwnerUserId, card.FamilyLinkId).Should().Be((1, Now.AddHours(2), Requester, LinkId));
        card.Ciphertext.Should().Equal(Ciphertext());
        link.Consents.Should().ContainSingle(c =>
            c.UserId == Requester && c.Purpose == FamilyLinkConsentPurpose.EmergencyCardShare && c.IsActive);
    }

    [Fact]
    public void ShareCard_NewerVersion_ReplacesWithoutDuplicatingConsent()
    {
        var link = Linked();
        link.ShareCard(Requester, 1, Key(), Nonce(), Ciphertext(), Now);

        var result = link.ShareCard(Requester, 2, Key(8), Nonce(), Ciphertext(32, 6), Now.AddHours(1));

        result.Value.Should().BeTrue();
        link.CardShares.Should().ContainSingle().Which.Version.Should().Be(2);
        link.Consents.Count(c => c.Purpose == FamilyLinkConsentPurpose.EmergencyCardShare).Should().Be(1);
    }

    [Fact]
    public void ShareCard_IdenticalReplay_SucceedsWithoutChange()
    {
        var link = Linked();
        link.ShareCard(Requester, 1, Key(), Nonce(), Ciphertext(), Now);
        var stamp = link.ConcurrencyStamp;

        var result = link.ShareCard(Requester, 1, Key(), Nonce(), Ciphertext(), Now.AddHours(1));

        result.Value.Should().BeFalse();
        link.ConcurrencyStamp.Should().Be(stamp);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ShareCard_NotNewerVersion_FailsStale(int version)
    {
        var link = Linked();
        link.ShareCard(Requester, 2, Key(), Nonce(), Ciphertext(), Now);

        var result = link.ShareCard(Requester, version, Key(), Nonce(), Ciphertext(10), Now);

        result.Error.Should().Be(FamilyLinkErrors.StaleCardVersion);
    }

    [Fact]
    public void ShareCard_ByStranger_FailsNotFound()
    {
        var result = Linked().ShareCard(Stranger, 1, Key(), Nonce(), Ciphertext(), Now);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public void ShareCard_WhilePending_FailsNotLinked()
    {
        var result = Pending().ShareCard(Requester, 1, Key(), Nonce(), Ciphertext(), Now);

        result.Error.Should().Be(FamilyLinkErrors.NotLinked);
    }

    public static TheoryData<int, int, int, int> MalformedEnvelopes => new()
    {
        { 0, 32, 12, 10 },
        { 1, 31, 12, 10 },
        { 1, 32, 11, 10 },
        { 1, 32, 12, 0 },
    };

    [Theory]
    [MemberData(nameof(MalformedEnvelopes))]
    public void ShareCard_MalformedEnvelope_FailsInvalidCard(int version, int keyLength, int nonceLength, int ctLength)
    {
        var result = Linked().ShareCard(
            Requester, version, new byte[keyLength], new byte[nonceLength], new byte[ctLength], Now);

        result.Error.Should().Be(FamilyLinkErrors.InvalidCard);
    }

    [Fact]
    public void ShareCard_OverSixteenKilobytes_FailsTooLarge()
    {
        var result = Linked().ShareCard(
            Requester, 1, Key(), Nonce(), new byte[FamilyLinkCardShare.MaxCiphertextBytes + 1], Now);

        result.Error.Should().Be(FamilyLinkErrors.CardTooLarge);
    }

    [Fact]
    public void ShareCard_ExactlySixteenKilobytes_IsAccepted()
    {
        var result = Linked().ShareCard(
            Requester, 1, Key(), Nonce(), new byte[FamilyLinkCardShare.MaxCiphertextBytes], Now);

        result.Value.Should().BeTrue();
    }

    [Fact]
    public void StopSharingCard_PurgesOwnCardOnlyAndWithdrawsShareConsent()
    {
        var link = Linked();
        link.ShareCard(Requester, 1, Key(), Nonce(), Ciphertext(), Now);
        link.ShareCard(Recipient, 1, Key(), Nonce(), Ciphertext(), Now);

        var result = link.StopSharingCard(Requester, Now.AddHours(3));

        result.Value.Should().BeTrue();
        link.CardOwnedBy(Requester).Should().BeNull();
        link.CardOwnedBy(Recipient).Should().NotBeNull();
        link.Consents.Single(c => c.UserId == Requester && c.Purpose == FamilyLinkConsentPurpose.EmergencyCardShare)
            .WithdrawnAt.Should().Be(Now.AddHours(3));
        link.Consents.Single(c => c.UserId == Recipient && c.Purpose == FamilyLinkConsentPurpose.EmergencyCardShare)
            .IsActive.Should().BeTrue();
    }

    [Fact]
    public void StopSharingCard_WhenNothingShared_IsIdempotent()
    {
        var result = Linked().StopSharingCard(Recipient, Now);

        result.Value.Should().BeFalse();
    }

    [Fact]
    public void StopSharingCard_ByStranger_FailsNotFound()
    {
        var result = Linked().StopSharingCard(Stranger, Now);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public void ShareCard_AfterStopping_RecordsAFreshConsent()
    {
        var link = Linked();
        link.ShareCard(Requester, 1, Key(), Nonce(), Ciphertext(), Now);
        link.StopSharingCard(Requester, Now);

        link.ShareCard(Requester, 1, Key(), Nonce(), Ciphertext(), Now.AddDays(1));

        link.Consents.Where(c => c.Purpose == FamilyLinkConsentPurpose.EmergencyCardShare)
            .Should().HaveCount(2).And.ContainSingle(c => c.IsActive);
    }

    [Fact]
    public void PartyHelpers_IdentifyBothSidesAndNobodyElse()
    {
        var link = Pending(Key(1));

        (link.IsParty(Requester), link.IsParty(Recipient), link.IsParty(Stranger)).Should().Be((true, true, false));
        (link.CounterpartOf(Requester), link.CounterpartOf(Recipient)).Should().Be((Recipient, Requester));
        link.PublicKeyOf(Requester).Should().Equal(Key(1));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(32, true)]
    [InlineData(0, false)]
    public void IsValidPublicKey_AcceptsOnly32Bytes(int? length, bool expected)
    {
        var valid = FamilyLink.IsValidPublicKey(length is null ? null : new byte[length.Value]);

        valid.Should().Be(expected);
    }

    [Theory]
    [InlineData("spouse", true)]
    [InlineData("neighbour", true)]
    [InlineData(null, false)]
    [InlineData("SPOUSE", false)]
    public void FamilyRelation_IsAllowed_MatchesTheAppWireCodes(string? relation, bool expected)
    {
        FamilyRelation.IsAllowed(relation).Should().Be(expected);
    }

    [Fact]
    public void AuditEntry_Record_CapturesTheTransition()
    {
        var entry = FamilyLinkAuditEntry.Record(
            LinkId, Recipient, FamilyLinkAuditAction.Approved, FamilyLinkStatus.Pending, FamilyLinkStatus.Linked, "corr-1", Now);

        (entry.FamilyLinkId, entry.ActorUserId, entry.Action, entry.FromStatus, entry.ToStatus, entry.CorrelationId, entry.OccurredAt)
            .Should().Be((LinkId, Recipient, FamilyLinkAuditAction.Approved, FamilyLinkStatus.Pending, FamilyLinkStatus.Linked, "corr-1", Now));
        entry.Id.Should().NotBeEmpty();
    }

    public static TheoryData<Guid> NonRecipients => new() { Requester, Stranger };
}
