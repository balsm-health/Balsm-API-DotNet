using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.Identity.Infrastructure.Handlers.FamilyLinks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using static Balsm.Identity.Tests.FamilyLinkFixtures;

namespace Balsm.Identity.Tests;

public sealed class FamilyLinkAccountEraserTests : IDisposable
{
    private static readonly Guid Outgoing = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    private static readonly Guid Incoming = Guid.Parse("00000000-0000-0000-0000-0000000000d2");
    private static readonly Guid Active = Guid.Parse("00000000-0000-0000-0000-0000000000d3");
    private readonly FamilyLinkHarness _h = new();

    public void Dispose() => _h.Dispose();

    private async Task RequestAsync(Guid id, Guid requester, Guid recipient)
    {
        var jti = Guid.NewGuid();
        _h.QrOwners.ResolveOwnerAsync(jti, Arg.Any<CancellationToken>()).Returns(recipient);
        await new CreateFamilyLinkHandler(_h.Db, _h.QrOwners, _h.Store, _h.Dtos)
            .Handle(new CreateFamilyLinkCommand(id, requester, jti, "other", null, null), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task EraseForAccount_EndsEveryOpenLinkAndPurgesCards()
    {
        var third = Guid.Parse("00000000-0000-0000-0000-000000000004");
        await RequestAsync(Outgoing, Requester, Recipient);
        await RequestAsync(Incoming, Stranger, Requester);
        await RequestAsync(Active, Requester, third);
        await new ApproveFamilyLinkHandler(_h.Store, _h.Dtos)
            .Handle(new ApproveFamilyLinkCommand(Active, third, null, null), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();
        await new UpsertFamilyLinkCardHandler(_h.Store).Handle(
            new UpsertFamilyLinkCardCommand(Active, third, 1, Key(), Nonce(), Ciphertext(), null), CancellationToken.None);
        _h.Db.ChangeTracker.Clear();

        var ended = await new FamilyLinkAccountEraser(_h.Db, new FixedClock(Now)).EraseForAccountAsync(Requester);

        ended.Should().Be(3);
        await using var fresh = _h.Fresh();
        var statuses = await fresh.FamilyLinks.ToDictionaryAsync(l => l.Id, l => l.Status);
        statuses.Should().BeEquivalentTo(new Dictionary<Guid, FamilyLinkStatus>
        {
            [Outgoing] = FamilyLinkStatus.Cancelled,
            [Incoming] = FamilyLinkStatus.Declined,
            [Active] = FamilyLinkStatus.Unlinked,
        });
        (await fresh.FamilyLinkCardShares.AnyAsync()).Should().BeFalse();
        (await fresh.FamilyLinkAuditEntries.CountAsync(a => a.CorrelationId == FamilyLinkAccountEraser.CorrelationId)).Should().Be(3);
    }

    [Fact]
    public async Task EraseForAccount_WithNoLinks_EndsNothing()
    {
        var ended = await new FamilyLinkAccountEraser(_h.Db, new FixedClock(Now)).EraseForAccountAsync(Stranger);

        ended.Should().Be(0);
    }
}
