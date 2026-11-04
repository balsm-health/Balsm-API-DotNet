using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>Stores a party's new device public key (e.g. after a phone change).</summary>
public sealed class UpdateFamilyLinkKeyHandler(FamilyLinkStore store) : IRequestHandler<UpdateFamilyLinkKeyCommand, Result>
{
    public async Task<Result> Handle(UpdateFamilyLinkKeyCommand cmd, CancellationToken ct)
    {
        var link = await store.LoadAsync(cmd.FamilyLinkId, ct).ConfigureAwait(false);
        if (link is null) return Result.Failure(FamilyLinkErrors.NotFound);

        var updated = link.SetPublicKey(cmd.UserId, cmd.PublicKey);
        if (updated.IsFailure) return updated;

        return await store.SaveAsync(link, cmd.UserId, FamilyLinkAuditAction.KeyUpdated, link.Status, cmd.CorrelationId, ct)
            .ConfigureAwait(false);
    }
}
