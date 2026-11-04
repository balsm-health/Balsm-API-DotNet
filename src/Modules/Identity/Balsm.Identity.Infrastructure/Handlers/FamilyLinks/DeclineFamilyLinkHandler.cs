using Balsm.Identity.Application.FamilyLinks.Commands;
using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>Recipient-only: pending → declined, consents withdrawn, audited.</summary>
public sealed class DeclineFamilyLinkHandler(FamilyLinkStore store, FamilyLinkDtoFactory dtos)
    : IRequestHandler<DeclineFamilyLinkCommand, Result<FamilyLinkDto>>
{
    public async Task<Result<FamilyLinkDto>> Handle(DeclineFamilyLinkCommand cmd, CancellationToken ct)
    {
        var link = await store.LoadAsync(cmd.FamilyLinkId, ct).ConfigureAwait(false);
        if (link is null) return Result.Failure<FamilyLinkDto>(FamilyLinkErrors.NotFound);

        var from = link.Status;
        var declined = link.Decline(cmd.UserId, store.UtcNow);
        if (declined.IsFailure) return Result.Failure<FamilyLinkDto>(declined.Error!);

        var saved = await store.SaveAsync(link, cmd.UserId, FamilyLinkAuditAction.Declined, from, cmd.CorrelationId, ct)
            .ConfigureAwait(false);
        if (saved.IsFailure) return Result.Failure<FamilyLinkDto>(saved.Error!);

        return Result.Success(await dtos.ToDtoAsync(link, cmd.UserId, ct).ConfigureAwait(false));
    }
}
