using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.Identity.Application.FamilyLinks.Queries;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.Data;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>
/// The card the other party sealed for the caller, while linked. Each read is
/// audited (who, when) even though the server only hands out ciphertext.
/// </summary>
public sealed class GetFamilyLinkCardHandler(IdentityDbContext db, FamilyLinkStore store)
    : IRequestHandler<GetFamilyLinkCardQuery, Result<FamilyLinkCardDto>>
{
    public async Task<Result<FamilyLinkCardDto>> Handle(GetFamilyLinkCardQuery query, CancellationToken ct)
    {
        var link = await db.FamilyLinks.AsNoTracking()
            .Include(l => l.CardShares)
            .FirstOrDefaultAsync(l => l.Id == query.FamilyLinkId, ct).ConfigureAwait(false);
        if (link is null || !link.IsParty(query.ViewerUserId) || link.Status != FamilyLinkStatus.Linked)
            return Result.Failure<FamilyLinkCardDto>(FamilyLinkErrors.NotFound);

        var card = link.CardOwnedBy(link.CounterpartOf(query.ViewerUserId));
        if (card is null) return Result.Failure<FamilyLinkCardDto>(FamilyLinkErrors.NotFound);

        var audited = await store.SaveAsync(
            link, query.ViewerUserId, FamilyLinkAuditAction.CardRead, link.Status, query.CorrelationId, ct).ConfigureAwait(false);
        if (audited.IsFailure) return Result.Failure<FamilyLinkCardDto>(audited.Error!);

        return Result.Success(new FamilyLinkCardDto(
            link.Id, card.OwnerUserId, card.Version, card.EphemeralPublicKey, card.Nonce, card.Ciphertext, card.UpdatedAt));
    }
}
