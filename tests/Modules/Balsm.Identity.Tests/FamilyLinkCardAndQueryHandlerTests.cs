using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Application.FamilyLinks.Queries;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.Handlers.FamilyLinks;
using Balsm.SharedKernel.Contracts;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using static Balsm.Identity.Tests.FamilyLinkFixtures;

namespace Balsm.Identity.Tests;

public sealed class FamilyLinkCardAndQueryHandlerTests : IDisposable
{
    private readonly FamilyLinkHarness _h = new();

    public void Dispose() => _h.Dispose();

    private async Task SeedPendingAsync(Guid? id = null, Guid? recipient = null)
    {
        var jti = Guid.NewGuid();
        _h.QrOwners.ResolveOwnerAsync(jti, Arg.Any<CancellationToken>()).Returns(recipient ?? Recipient);
        await new CreateFamilyLinkHandler(_h.Db, _h.QrOwners, _h.Store, _h.Dtos).Handle(
            new CreateFamilyLinkCommand(id ?? LinkId, Requester, jti, "child", Key(1), null), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();
    }

    private async Task SeedLinkedAsync()
    {
        await SeedPendingAsync();
        await new ApproveFamilyLinkHandler(_h.Store, _h.Dtos)
            .Handle(new ApproveFamilyLinkCommand(LinkId, Recipient, Key(2), null), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();
    }

    private Task<Balsm.SharedKernel.Results.Result> ShareAsync(Guid owner, int version, int length = 64) =>
        new UpsertFamilyLinkCardHandler(_h.Store).Handle(
            new UpsertFamilyLinkCardCommand(LinkId, owner, version, Key(), Nonce(), Ciphertext(length), null),
            CancellationToken.None);

    [Fact]
    public async Task UpsertCard_OnLinkedLink_StoresAndAudits()
    {
        await SeedLinkedAsync();

        var result = await ShareAsync(Requester, 1);

        result.IsSuccess.Should().BeTrue();
        await using var fresh = _h.Fresh();
        (await fresh.FamilyLinkCardShares.SingleAsync()).Version.Should().Be(1);
        (await fresh.FamilyLinkAuditEntries.CountAsync(a => a.Action == FamilyLinkAuditAction.CardShared)).Should().Be(1);
    }

    [Fact]
    public async Task UpsertCard_IdenticalReplay_SucceedsWithoutAudit()
    {
        await SeedLinkedAsync();
        await ShareAsync(Requester, 1);
        _h.Db.ChangeTracker.Clear();

        var result = await ShareAsync(Requester, 1);

        result.IsSuccess.Should().BeTrue();
        (await _h.Fresh().FamilyLinkAuditEntries.CountAsync(a => a.Action == FamilyLinkAuditAction.CardShared)).Should().Be(1);
    }

    [Fact]
    public async Task UpsertCard_StaleVersion_FailsStale()
    {
        await SeedLinkedAsync();
        await ShareAsync(Requester, 3);
        _h.Db.ChangeTracker.Clear();

        var result = await ShareAsync(Requester, 2, 10);

        result.Error.Should().Be(FamilyLinkErrors.StaleCardVersion);
    }

    [Fact]
    public async Task UpsertCard_UnknownLink_FailsNotFound()
    {
        var result = await ShareAsync(Requester, 1);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task UpdateKey_ByParty_StoresKeyAndAudits()
    {
        await SeedPendingAsync();

        var result = await new UpdateFamilyLinkKeyHandler(_h.Store)
            .Handle(new UpdateFamilyLinkKeyCommand(LinkId, Recipient, Key(6), null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await using var fresh = _h.Fresh();
        (await fresh.FamilyLinks.SingleAsync()).RecipientPublicKey.Should().Equal(Key(6));
        (await fresh.FamilyLinkAuditEntries.CountAsync(a => a.Action == FamilyLinkAuditAction.KeyUpdated)).Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateKey_ByStrangerOrUnknownId_FailsNotFound(bool unknownId)
    {
        await SeedPendingAsync();

        var result = await new UpdateFamilyLinkKeyHandler(_h.Store).Handle(
            new UpdateFamilyLinkKeyCommand(unknownId ? Guid.NewGuid() : LinkId, unknownId ? Requester : Stranger, Key(), null),
            CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task DeleteCard_ByOwner_PurgesAndAudits()
    {
        await SeedLinkedAsync();
        await ShareAsync(Recipient, 1);
        _h.Db.ChangeTracker.Clear();

        var result = await new DeleteFamilyLinkCardHandler(_h.Store)
            .Handle(new DeleteFamilyLinkCardCommand(LinkId, Recipient, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await using var fresh = _h.Fresh();
        (await fresh.FamilyLinkCardShares.AnyAsync()).Should().BeFalse();
        (await fresh.FamilyLinkAuditEntries.CountAsync(a => a.Action == FamilyLinkAuditAction.CardRevoked)).Should().Be(1);
    }

    [Fact]
    public async Task DeleteCard_NothingShared_IsIdempotent()
    {
        await SeedLinkedAsync();

        var result = await new DeleteFamilyLinkCardHandler(_h.Store)
            .Handle(new DeleteFamilyLinkCardCommand(LinkId, Recipient, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteCard_ByStrangerOrUnknownId_FailsNotFound(bool unknownId)
    {
        await SeedLinkedAsync();

        var result = await new DeleteFamilyLinkCardHandler(_h.Store).Handle(
            new DeleteFamilyLinkCardCommand(unknownId ? Guid.NewGuid() : LinkId, unknownId ? Requester : Stranger, null),
            CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task GetCard_ByViewer_ReturnsTheOtherSidesCardAndAuditsTheRead()
    {
        await SeedLinkedAsync();
        await ShareAsync(Recipient, 2);
        _h.Db.ChangeTracker.Clear();

        var result = await new GetFamilyLinkCardHandler(_h.Db, _h.Store)
            .Handle(new GetFamilyLinkCardQuery(LinkId, Requester, "corr-9"), CancellationToken.None);

        var card = result.Value!;
        (card.FamilyLinkId, card.OwnerUserId, card.Version, card.UpdatedAt).Should().Be((LinkId, Recipient, 2, Now));
        card.Ciphertext.Should().Equal(Ciphertext());
        (await _h.Fresh().FamilyLinkAuditEntries.SingleAsync(a => a.Action == FamilyLinkAuditAction.CardRead))
            .Should().Match<FamilyLinkAuditEntry>(a => a.ActorUserId == Requester && a.CorrelationId == "corr-9");
    }

    [Fact]
    public async Task GetCard_OwnCardOnly_FailsNotFound()
    {
        await SeedLinkedAsync();
        await ShareAsync(Requester, 1);
        _h.Db.ChangeTracker.Clear();

        var result = await new GetFamilyLinkCardHandler(_h.Db, _h.Store)
            .Handle(new GetFamilyLinkCardQuery(LinkId, Requester, null), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task GetCard_WhilePending_FailsNotFound()
    {
        await SeedPendingAsync();

        var result = await new GetFamilyLinkCardHandler(_h.Db, _h.Store)
            .Handle(new GetFamilyLinkCardQuery(LinkId, Requester, null), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetCard_ByStrangerOrUnknownId_FailsNotFound(bool unknownId)
    {
        await SeedLinkedAsync();
        await ShareAsync(Recipient, 1);

        var result = await new GetFamilyLinkCardHandler(_h.Db, _h.Store).Handle(
            new GetFamilyLinkCardQuery(unknownId ? Guid.NewGuid() : LinkId, unknownId ? Requester : Stranger, null),
            CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task GetById_RequesterBeforeApproval_SeesNothingAboutTheRecipient()
    {
        await SeedPendingAsync();

        var result = await new GetFamilyLinkByIdHandler(_h.Db, _h.Dtos)
            .Handle(new GetFamilyLinkByIdQuery(LinkId, Requester), CancellationToken.None);

        result.Value!.Counterpart.Should().BeNull();
        await _h.Names.DidNotReceive().GetAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_RecipientBeforeApproval_SeesRequesterNamesButNoAccountId()
    {
        await SeedPendingAsync();

        var result = await new GetFamilyLinkByIdHandler(_h.Db, _h.Dtos)
            .Handle(new GetFamilyLinkByIdQuery(LinkId, Recipient), CancellationToken.None);

        result.Value!.Counterpart.Should().Be(new Application.FamilyLinks.Dtos.FamilyLinkCounterpartDto(null, "handle1", "Member 1"));
        result.Value.CounterpartPublicKey.Should().BeNull();
    }

    [Fact]
    public async Task GetById_WhenCounterpartHasNoAccountRow_RevealsIdWithoutNames()
    {
        await SeedLinkedAsync();
        _h.Names.GetAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, AccountDisplayName>());

        var result = await new GetFamilyLinkByIdHandler(_h.Db, _h.Dtos)
            .Handle(new GetFamilyLinkByIdQuery(LinkId, Requester), CancellationToken.None);

        result.Value!.Counterpart.Should().Be(new Application.FamilyLinks.Dtos.FamilyLinkCounterpartDto(Recipient, null, null));
    }

    [Fact]
    public async Task GetById_WhileLinked_ShowsBothCardVersionsFromTheRightSide()
    {
        await SeedLinkedAsync();
        await ShareAsync(Requester, 4);
        _h.Db.ChangeTracker.Clear();
        await ShareAsync(Recipient, 7);

        var asRequester = (await new GetFamilyLinkByIdHandler(_h.Db, _h.Dtos)
            .Handle(new GetFamilyLinkByIdQuery(LinkId, Requester), CancellationToken.None)).Value!;
        var asRecipient = (await new GetFamilyLinkByIdHandler(_h.Db, _h.Dtos)
            .Handle(new GetFamilyLinkByIdQuery(LinkId, Recipient), CancellationToken.None)).Value!;

        (asRequester.MyCardVersion, asRequester.TheirCardVersion).Should().Be((4, 7));
        (asRecipient.MyCardVersion, asRecipient.TheirCardVersion).Should().Be((7, 4));
        asRequester.CounterpartPublicKey.Should().Equal(Key(2));
    }

    [Fact]
    public async Task GetById_ByStranger_FailsNotFound()
    {
        await SeedPendingAsync();

        var result = await new GetFamilyLinkByIdHandler(_h.Db, _h.Dtos)
            .Handle(new GetFamilyLinkByIdQuery(LinkId, Stranger), CancellationToken.None);

        result.Error.Should().Be(FamilyLinkErrors.NotFound);
    }

    [Fact]
    public async Task ListIncoming_FiltersByStatusAndPagesNewestFirst()
    {
        await SeedPendingAsync(Guid.Parse("00000000-0000-0000-0000-0000000000b1"));
        await SeedPendingAsync(Guid.Parse("00000000-0000-0000-0000-0000000000b2"), Stranger);
        await new DeleteFamilyLinkHandler(_h.Store).Handle(
            new DeleteFamilyLinkCommand(Guid.Parse("00000000-0000-0000-0000-0000000000b1"), Requester, null), CancellationToken.None);

        var pending = (await new ListIncomingFamilyLinksHandler(_h.Db, _h.Dtos)
            .Handle(new ListIncomingFamilyLinksQuery(Recipient, "pending", 1, 20), CancellationToken.None)).Value!;
        var all = (await new ListIncomingFamilyLinksHandler(_h.Db, _h.Dtos)
            .Handle(new ListIncomingFamilyLinksQuery(Recipient, null, 1, 20), CancellationToken.None)).Value!;

        pending.TotalCount.Should().Be(0);
        all.Items.Should().ContainSingle().Which.Status.Should().Be("cancelled");
    }

    [Fact]
    public async Task ListOutgoing_PagesAtTheDatabase()
    {
        await SeedPendingAsync(Guid.Parse("00000000-0000-0000-0000-0000000000c1"));
        await SeedPendingAsync(Guid.Parse("00000000-0000-0000-0000-0000000000c2"), Stranger);

        var page = (await new ListOutgoingFamilyLinksHandler(_h.Db, _h.Dtos)
            .Handle(new ListOutgoingFamilyLinksQuery(Requester, "pending", 2, 1), CancellationToken.None)).Value!;

        (page.TotalCount, page.Items.Count, page.HasNextPage, page.HasPreviousPage).Should().Be((2, 1, false, true));
        page.Items[0].Should().Match<Application.FamilyLinks.Dtos.FamilyLinkDto>(d => d.Direction == "outgoing" && d.Counterpart == null);
    }

    [Fact]
    public async Task ListOutgoing_OtherAccounts_SeeNothing()
    {
        await SeedPendingAsync();

        var page = (await new ListOutgoingFamilyLinksHandler(_h.Db, _h.Dtos)
            .Handle(new ListOutgoingFamilyLinksQuery(Stranger, null, 1, 20), CancellationToken.None)).Value!;

        page.TotalCount.Should().Be(0);
    }
}
