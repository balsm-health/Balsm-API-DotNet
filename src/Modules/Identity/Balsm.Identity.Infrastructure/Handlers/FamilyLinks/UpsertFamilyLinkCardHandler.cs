using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>Owner-only: stores a newer sealed card; an identical replay is a no-op.</summary>
public sealed class UpsertFamilyLinkCardHandler(FamilyLinkStore store) : IRequestHandler<UpsertFamilyLinkCardCommand, Result>
{
    public async Task<Result> Handle(UpsertFamilyLinkCardCommand cmd, CancellationToken ct)
    {
        var link = await store.LoadAsync(cmd.FamilyLinkId, ct).ConfigureAwait(false);
        if (link is null) return Result.Failure(FamilyLinkErrors.NotFound);

        var shared = link.ShareCard(
            cmd.OwnerUserId, cmd.Version, cmd.EphemeralPublicKey, cmd.Nonce, cmd.Ciphertext, store.UtcNow);
        if (shared.IsFailure) return Result.Failure(shared.Error!);
        if (!shared.Value) return Result.Success();

        return await store.SaveAsync(link, cmd.OwnerUserId, FamilyLinkAuditAction.CardShared, link.Status, cmd.CorrelationId, ct)
            .ConfigureAwait(false);
    }
}
