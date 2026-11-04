using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.Data;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Contracts;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>
/// Opens a pending link from the caller to the owner of a scanned profile QR.
/// The jti is resolved once, here; the link then binds to account ids.
/// </summary>
public sealed class CreateFamilyLinkHandler(
    IdentityDbContext db,
    IProfileQrOwnerResolver qrOwners,
    FamilyLinkStore store,
    FamilyLinkDtoFactory dtos)
    : IRequestHandler<CreateFamilyLinkCommand, Result<CreateFamilyLinkResult>>
{
    public async Task<Result<CreateFamilyLinkResult>> Handle(CreateFamilyLinkCommand cmd, CancellationToken ct)
    {
        var replay = await ReplayAsync(cmd, ct).ConfigureAwait(false);
        if (replay is not null) return replay;

        // Unknown, revoked and expired tokens all resolve to null: one 404.
        var recipient = await qrOwners.ResolveOwnerAsync(cmd.Jti, ct).ConfigureAwait(false);
        if (recipient is null) return Result.Failure<CreateFamilyLinkResult>(FamilyLinkErrors.NotFound);

        var requested = FamilyLink.Request(
            cmd.Id, cmd.RequesterUserId, recipient.Value, cmd.Relation, cmd.PublicKey, store.UtcNow);
        if (requested.IsFailure) return Result.Failure<CreateFamilyLinkResult>(requested.Error!);
        var link = requested.Value!;

        var pairTaken = await db.FamilyLinks.AsNoTracking().AnyAsync(l =>
            l.PairLowUserId == link.PairLowUserId
            && l.PairHighUserId == link.PairHighUserId
            && (l.Status == FamilyLinkStatus.Pending || l.Status == FamilyLinkStatus.Linked), ct).ConfigureAwait(false);
        if (pairTaken) return Result.Failure<CreateFamilyLinkResult>(FamilyLinkErrors.AlreadyExists);

        db.FamilyLinks.Add(link);
        var saved = await store.SaveAsync(
            link, cmd.RequesterUserId, FamilyLinkAuditAction.Requested, null, cmd.CorrelationId, ct).ConfigureAwait(false);
        if (saved.IsFailure)
        {
            // Lost a race: either our own retry landed first (replay) or the
            // open-pair index caught a concurrent request for the same pair.
            return await ReplayAsync(cmd, ct).ConfigureAwait(false)
                ?? Result.Failure<CreateFamilyLinkResult>(FamilyLinkErrors.AlreadyExists);
        }

        return Result.Success(new CreateFamilyLinkResult(
            await dtos.ToDtoAsync(link, cmd.RequesterUserId, ct).ConfigureAwait(false), Created: true));
    }

    /// <summary>Idempotency on the client-minted id. Another requester's id answers
    /// 404 like an unknown jti, so the replay path cannot probe for links.</summary>
    private async Task<Result<CreateFamilyLinkResult>?> ReplayAsync(CreateFamilyLinkCommand cmd, CancellationToken ct)
    {
        var existing = await db.FamilyLinks.AsNoTracking()
            .Include(l => l.CardShares)
            .FirstOrDefaultAsync(l => l.Id == cmd.Id, ct).ConfigureAwait(false);
        if (existing is null) return null;
        if (existing.RequesterUserId != cmd.RequesterUserId)
            return Result.Failure<CreateFamilyLinkResult>(FamilyLinkErrors.NotFound);

        return Result.Success(new CreateFamilyLinkResult(
            await dtos.ToDtoAsync(existing, cmd.RequesterUserId, ct).ConfigureAwait(false), Created: false));
    }
}
