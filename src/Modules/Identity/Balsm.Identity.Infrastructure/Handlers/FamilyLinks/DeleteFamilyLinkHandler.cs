using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>Cancel (requester, pending) or unlink (either side, linked — cards purged). Idempotent once ended.</summary>
public sealed class DeleteFamilyLinkHandler(FamilyLinkStore store) : IRequestHandler<DeleteFamilyLinkCommand, Result>
{
    public async Task<Result> Handle(DeleteFamilyLinkCommand cmd, CancellationToken ct)
    {
        var link = await store.LoadAsync(cmd.FamilyLinkId, ct).ConfigureAwait(false);
        if (link is null) return Result.Failure(FamilyLinkErrors.NotFound);

        var from = link.Status;
        var ended = link.End(cmd.UserId, store.UtcNow);
        if (ended.IsFailure) return Result.Failure(ended.Error!);
        if (!ended.Value) return Result.Success();

        var action = from == FamilyLinkStatus.Pending ? FamilyLinkAuditAction.Cancelled : FamilyLinkAuditAction.Unlinked;
        return await store.SaveAsync(link, cmd.UserId, action, from, cmd.CorrelationId, ct).ConfigureAwait(false);
    }
}
