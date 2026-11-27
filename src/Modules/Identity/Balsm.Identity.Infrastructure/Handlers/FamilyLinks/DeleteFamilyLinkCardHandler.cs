using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>Owner-only: purges the sealed card and withdraws the share consent. Idempotent.</summary>
public sealed class DeleteFamilyLinkCardHandler(FamilyLinkStore store) : IRequestHandler<DeleteFamilyLinkCardCommand, Result>
{
    public async Task<Result> Handle(DeleteFamilyLinkCardCommand cmd, CancellationToken ct)
    {
        var link = await store.LoadAsync(cmd.FamilyLinkId, ct).ConfigureAwait(false);
        if (link is null) return Result.Failure(FamilyLinkErrors.NotFound);

        var stopped = link.StopSharingCard(cmd.OwnerUserId, store.UtcNow);
        if (stopped.IsFailure) return Result.Failure(stopped.Error!);
        if (!stopped.Value) return Result.Success();

        return await store.SaveAsync(link, cmd.OwnerUserId, FamilyLinkAuditAction.CardRevoked, link.Status, cmd.CorrelationId, ct)
            .ConfigureAwait(false);
    }
}
