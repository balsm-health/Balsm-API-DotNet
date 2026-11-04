using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.Handlers.FamilyLinks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using static Balsm.Identity.Tests.FamilyLinkFixtures;

namespace Balsm.Identity.Tests;

public sealed class FamilyLinkCommandHandlerTests : IDisposable
{
    private readonly FamilyLinkHarness _h = new();

    public void Dispose() => _h.Dispose();

    private CreateFamilyLinkHandler Create => new(_h.Db, _h.QrOwners, _h.Store, _h.Dtos);

    private static CreateFamilyLinkCommand Request(Guid? id = null, Guid? requester = null) =>
        new(id ?? LinkId, requester ?? Requester, Jti, "child", Key(1), "corr-1");

    private async Task SeedLinkedAsync()
    {
        await Create.Handle(Request(), CancellationToken.None);
        await new ApproveFamilyLinkHandler(_h.Store, _h.Dtos)
            .Handle(new ApproveFamilyLinkCommand(LinkId, Recipient, Key(2), "corr-2"), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();
    }

    private static string Sql(Guid g) => g.ToString().ToUpperInvariant();

    [Fact]
    public async Task Create_WithActiveJti_OpensPendingLinkAndAuditsIt()
    {
        var result = await Create.Handle(Request(), CancellationToken.None);

        result.Value!.Created.Should().BeTrue();
        result.Value.Link.Should().Match<Application.FamilyLinks.Dtos.FamilyLinkDto>(d =>
            d.Status == "pending" && d.Direction == "outgoing" && d.Counterpart == null && d.CounterpartPublicKey == null);
        await using var fresh = _h.Fresh();
        (await fresh.FamilyLinkAuditEntries.SingleAsync()).Should().Match<FamilyLinkAuditEntry>(a =>
            a.Action == FamilyLinkAuditAction.Requested && a.FromStatus == null && a.ToStatus == FamilyLinkStatus.Pending
            && a.ActorUserId == Requester && a.CorrelationId == "corr-1" && a.OccurredAt == Now);
        (await fresh.FamilyLinkConsents.SingleAsync()).UserId.Should().Be(Requester);
    }

    [Fact]
    public async Task Create_WhenJtiDoesNotResolve_FailsNotFound()
    {
        _h.QrOwners.ResolveOwnerAsync(Jti, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var result = await Create.Handle(Request(), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task Create_OwnQr_FailsSelfLink()
    {
        _h.QrOwners.ResolveOwnerAsync(Jti, Arg.Any<CancellationToken>()).Returns(Requester);

        var result = await Create.Handle(Request(), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.SelfLink);
    }

    [Fact]
    public async Task Create_ReplayOfSameId_ReturnsExistingWithoutDuplicating()
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var replay = await Create.Handle(Request(), CancellationToken.None);

        replay.Value!.Created.Should().BeFalse();
        replay.Value.Link.Id.Should().Be(LinkId);
        (await _h.Fresh().FamilyLinks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Create_IdOwnedByAnotherRequester_FailsNotFound()
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await Create.Handle(Request(requester: Stranger), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task Create_WhenPairAlreadyOpenInReverse_FailsAlreadyExists()
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();
        var reverseJti = Guid.NewGuid();
        _h.QrOwners.ResolveOwnerAsync(reverseJti, Arg.Any<CancellationToken>()).Returns(Requester);

        var result = await Create.Handle(
            new CreateFamilyLinkCommand(Guid.NewGuid(), Recipient, reverseJti, "parent", null, null), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.AlreadyExists);
    }

    [Fact]
    public async Task Create_AfterDecline_AllowsANewRequest()
    {
        await Create.Handle(Request(), CancellationToken.None);
        await new DeclineFamilyLinkHandler(_h.Store, _h.Dtos)
            .Handle(new DeclineFamilyLinkCommand(LinkId, Recipient, null), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await Create.Handle(Request(id: Guid.NewGuid()), CancellationToken.None);

        result.Value!.Created.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithInvalidRelationReachingTheDomain_FailsInvalidRelation()
    {
        var result = await Create.Handle(
            new CreateFamilyLinkCommand(LinkId, Requester, Jti, "cousin", null, null), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.InvalidRelation);
    }

    [Fact]
    public async Task Create_LosingThePairRaceToAConcurrentRequest_FailsAlreadyExists()
    {
        var rival = Guid.NewGuid();
        var (low, high) = Requester.CompareTo(Recipient) < 0 ? (Requester, Recipient) : (Recipient, Requester);
        _h.BeforeNextSave(
            "INSERT INTO family_link (id, requester_user_id, recipient_user_id, pair_low_user_id, pair_high_user_id, " +
            "relation, status, concurrency_stamp, created_at) VALUES " +
            $"('{Sql(rival)}', '{Sql(Recipient)}', '{Sql(Requester)}', '{Sql(low)}', '{Sql(high)}', 'parent', 'pending', " +
            $"'{Sql(Guid.NewGuid())}', '2025-01-01 00:00:00')");

        var result = await Create.Handle(Request(), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.AlreadyExists);
    }

    [Fact]
    public async Task Create_LosingTheRaceToOwnRetry_ReturnsTheReplay()
    {
        var (low, high) = Requester.CompareTo(Recipient) < 0 ? (Requester, Recipient) : (Recipient, Requester);
        _h.BeforeNextSave(
            "INSERT INTO family_link (id, requester_user_id, recipient_user_id, pair_low_user_id, pair_high_user_id, " +
            "relation, status, concurrency_stamp, created_at) VALUES " +
            $"('{Sql(LinkId)}', '{Sql(Requester)}', '{Sql(Recipient)}', '{Sql(low)}', '{Sql(high)}', 'child', 'pending', " +
            $"'{Sql(Guid.NewGuid())}', '2025-01-01 00:00:00')");

        var result = await Create.Handle(Request(), CancellationToken.None);

        result.Value!.Created.Should().BeFalse();
    }

    [Fact]
    public async Task Approve_ByRecipient_LinksAndRevealsTheRequester()
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await new ApproveFamilyLinkHandler(_h.Store, _h.Dtos)
            .Handle(new ApproveFamilyLinkCommand(LinkId, Recipient, Key(2), "corr-2"), CancellationToken.None);

        var dto = result.Value!;
        (dto.Status, dto.Direction).Should().Be(("linked", "incoming"));
        dto.Counterpart.Should().Be(new Application.FamilyLinks.Dtos.FamilyLinkCounterpartDto(Requester, "handle1", "Member 1"));
        dto.CounterpartPublicKey.Should().Equal(Key(1));
        dto.OwnPublicKey.Should().Equal(Key(2));
        (await _h.Fresh().FamilyLinkAuditEntries.CountAsync(a => a.Action == FamilyLinkAuditAction.Approved)).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approve_ByRequesterOrUnknownId_FailsNotFound(bool unknownId)
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await new ApproveFamilyLinkHandler(_h.Store, _h.Dtos).Handle(
            new ApproveFamilyLinkCommand(unknownId ? Guid.NewGuid() : LinkId, unknownId ? Recipient : Requester, null, null),
            CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task Approve_WhenTheLinkChangedMeanwhile_FailsConcurrentUpdate()
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();
        _h.BeforeNextSave($"UPDATE family_link SET concurrency_stamp = '{Sql(Guid.NewGuid())}'");

        var result = await new ApproveFamilyLinkHandler(_h.Store, _h.Dtos)
            .Handle(new ApproveFamilyLinkCommand(LinkId, Recipient, null, null), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.ConcurrentUpdate);
    }

    [Fact]
    public async Task Decline_ByRecipient_DeclinesAndAudits()
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await new DeclineFamilyLinkHandler(_h.Store, _h.Dtos)
            .Handle(new DeclineFamilyLinkCommand(LinkId, Recipient, null), CancellationToken.None);

        result.Value!.Status.Should().Be("declined");
        (await _h.Fresh().FamilyLinkAuditEntries.CountAsync(a => a.Action == FamilyLinkAuditAction.Declined)).Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Decline_ByRequesterOrUnknownId_FailsNotFound(bool unknownId)
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await new DeclineFamilyLinkHandler(_h.Store, _h.Dtos).Handle(
            new DeclineFamilyLinkCommand(unknownId ? Guid.NewGuid() : LinkId, unknownId ? Recipient : Requester, null),
            CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task Decline_WhenTheLinkChangedMeanwhile_FailsConcurrentUpdate()
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();
        _h.BeforeNextSave($"UPDATE family_link SET concurrency_stamp = '{Sql(Guid.NewGuid())}'");

        var result = await new DeclineFamilyLinkHandler(_h.Store, _h.Dtos)
            .Handle(new DeclineFamilyLinkCommand(LinkId, Recipient, null), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.ConcurrentUpdate);
    }

    [Fact]
    public async Task Delete_PendingByRequester_CancelsAndAuditsCancelled()
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await new DeleteFamilyLinkHandler(_h.Store)
            .Handle(new DeleteFamilyLinkCommand(LinkId, Requester, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await using var fresh = _h.Fresh();
        (await fresh.FamilyLinks.SingleAsync()).Status.Should().Be(FamilyLinkStatus.Cancelled);
        (await fresh.FamilyLinkAuditEntries.CountAsync(a => a.Action == FamilyLinkAuditAction.Cancelled)).Should().Be(1);
    }

    [Fact]
    public async Task Delete_LinkedByRecipient_UnlinksAndPurgesCardRows()
    {
        await SeedLinkedAsync();
        await new UpsertFamilyLinkCardHandler(_h.Store).Handle(
            new UpsertFamilyLinkCardCommand(LinkId, Requester, 1, Key(), Nonce(), Ciphertext(), null), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await new DeleteFamilyLinkHandler(_h.Store)
            .Handle(new DeleteFamilyLinkCommand(LinkId, Recipient, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await using var fresh = _h.Fresh();
        (await fresh.FamilyLinkCardShares.AnyAsync()).Should().BeFalse();
        (await fresh.FamilyLinks.SingleAsync()).Status.Should().Be(FamilyLinkStatus.Unlinked);
        (await fresh.FamilyLinkAuditEntries.CountAsync(a => a.Action == FamilyLinkAuditAction.Unlinked)).Should().Be(1);
    }

    [Fact]
    public async Task Delete_AlreadyEnded_SucceedsWithoutAnotherAuditRow()
    {
        await Create.Handle(Request(), CancellationToken.None);
        await new DeleteFamilyLinkHandler(_h.Store).Handle(new DeleteFamilyLinkCommand(LinkId, Requester, null), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await new DeleteFamilyLinkHandler(_h.Store)
            .Handle(new DeleteFamilyLinkCommand(LinkId, Requester, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _h.Fresh().FamilyLinkAuditEntries.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_ByStrangerOrUnknownId_FailsNotFound(bool unknownId)
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await new DeleteFamilyLinkHandler(_h.Store).Handle(
            new DeleteFamilyLinkCommand(unknownId ? Guid.NewGuid() : LinkId, unknownId ? Requester : Stranger, null),
            CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task Delete_PendingByRecipient_FailsRecipientMustDecline()
    {
        await Create.Handle(Request(), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var result = await new DeleteFamilyLinkHandler(_h.Store)
            .Handle(new DeleteFamilyLinkCommand(LinkId, Recipient, null), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.RecipientMustDecline);
    }
}
