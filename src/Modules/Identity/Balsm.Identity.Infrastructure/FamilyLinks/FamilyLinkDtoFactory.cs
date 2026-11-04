using Balsm.Identity.Application.FamilyLinks;
using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.SharedKernel.Contracts;

namespace Balsm.Identity.Infrastructure.FamilyLinks;

/// <summary>
/// Builds the caller's view of links and applies the reveal rules in one place:
/// the recipient always sees who is asking; the requester sees the recipient
/// only after approval; account ids only once linked; the other side's key and
/// card version only while linked.
/// </summary>
public sealed class FamilyLinkDtoFactory(IAccountDisplayNameReader names)
{
    internal async Task<IReadOnlyList<FamilyLinkDto>> ToDtosAsync(
        IReadOnlyList<FamilyLinkView> links, Guid viewerUserId, CancellationToken ct)
    {
        var revealed = links
            .Where(l => Reveals(l, viewerUserId))
            .Select(l => CounterpartOf(l, viewerUserId))
            .Distinct()
            .ToList();
        var resolved = revealed.Count == 0
            ? new Dictionary<Guid, AccountDisplayName>()
            : await names.GetAsync(revealed, ct).ConfigureAwait(false);

        return links.Select(l => ToDto(l, viewerUserId, resolved)).ToList();
    }

    internal async Task<FamilyLinkDto> ToDtoAsync(FamilyLink link, Guid viewerUserId, CancellationToken ct) =>
        (await ToDtosAsync([FamilyLinkView.From(link)], viewerUserId, ct).ConfigureAwait(false))[0];

    private static FamilyLinkDto ToDto(
        FamilyLinkView l, Guid viewer, IReadOnlyDictionary<Guid, AccountDisplayName> resolved)
    {
        var outgoing = viewer == l.RequesterUserId;
        var counterpartId = CounterpartOf(l, viewer);
        var linked = l.Status == FamilyLinkStatus.Linked;

        FamilyLinkCounterpartDto? counterpart = null;
        if (Reveals(l, viewer))
        {
            resolved.TryGetValue(counterpartId, out var name);
            counterpart = new FamilyLinkCounterpartDto(
                l.LinkedAt is null ? null : counterpartId, name?.Handle, name?.DisplayName);
        }

        return new FamilyLinkDto(
            l.Id,
            outgoing ? FamilyLinkWire.Outgoing : FamilyLinkWire.Incoming,
            FamilyLinkWire.ToWire(l.Status),
            l.Relation,
            counterpart,
            linked ? (outgoing ? l.RecipientPublicKey : l.RequesterPublicKey) : null,
            outgoing ? l.RequesterPublicKey : l.RecipientPublicKey,
            outgoing ? l.RequesterCardVersion : l.RecipientCardVersion,
            linked ? (outgoing ? l.RecipientCardVersion : l.RequesterCardVersion) : null,
            l.CreatedAt,
            l.RespondedAt,
            l.LinkedAt,
            l.EndedAt);
    }

    private static bool Reveals(FamilyLinkView l, Guid viewer) =>
        viewer == l.RecipientUserId || l.LinkedAt is not null;

    private static Guid CounterpartOf(FamilyLinkView l, Guid viewer) =>
        viewer == l.RequesterUserId ? l.RecipientUserId : l.RequesterUserId;
}
