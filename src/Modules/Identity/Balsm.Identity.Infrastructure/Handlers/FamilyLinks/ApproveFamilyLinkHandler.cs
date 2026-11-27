using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>Recipient-only: pending → linked, recipient consent and audit in one transaction.</summary>
public sealed class ApproveFamilyLinkHandler(FamilyLinkStore store, FamilyLinkDtoFactory dtos)
    : IRequestHandler<ApproveFamilyLinkCommand, Result<FamilyLinkDto>>
{
    public async Task<Result<FamilyLinkDto>> Handle(ApproveFamilyLinkCommand cmd, CancellationToken ct)
    {
        var link = await store.LoadAsync(cmd.FamilyLinkId, ct).ConfigureAwait(false);
        if (link is null) return Result.Failure<FamilyLinkDto>(FamilyLinkErrors.NotFound);

        var from = link.Status;
        var approved = link.Approve(cmd.UserId, cmd.PublicKey, store.UtcNow);
        if (approved.IsFailure) return Result.Failure<FamilyLinkDto>(approved.Error!);

        var saved = await store.SaveAsync(link, cmd.UserId, FamilyLinkAuditAction.Approved, from, cmd.CorrelationId, ct)
            .ConfigureAwait(false);
        if (saved.IsFailure) return Result.Failure<FamilyLinkDto>(saved.Error!);

        return Result.Success(await dtos.ToDtoAsync(link, cmd.UserId, ct).ConfigureAwait(false));
    }
}
