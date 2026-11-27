using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.Identity.Application.FamilyLinks.Queries;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.Identity.Infrastructure.Data;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>Party-only read of one link; non-parties get the same 404 as an unknown id.</summary>
public sealed class GetFamilyLinkByIdHandler(IdentityDbContext db, FamilyLinkDtoFactory dtos)
    : IRequestHandler<GetFamilyLinkByIdQuery, Result<FamilyLinkDto>>
{
    public async Task<Result<FamilyLinkDto>> Handle(GetFamilyLinkByIdQuery query, CancellationToken ct)
    {
        var view = await db.FamilyLinks.AsNoTracking()
            .Where(l => l.Id == query.FamilyLinkId
                        && (l.RequesterUserId == query.UserId || l.RecipientUserId == query.UserId))
            .Select(FamilyLinkProjection.ToView)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (view is null) return Result.Failure<FamilyLinkDto>(FamilyLinkErrors.NotFound);

        return Result.Success((await dtos.ToDtosAsync([view], query.UserId, ct).ConfigureAwait(false))[0]);
    }
}
