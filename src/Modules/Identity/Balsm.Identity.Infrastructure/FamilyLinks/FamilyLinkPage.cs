using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.Identity.Domain.FamilyLinks;
using Balsm.SharedKernel.Pagination;
using Balsm.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Balsm.Identity.Infrastructure.FamilyLinks;

/// <summary>Database-side paging (newest first) shared by the incoming and outgoing lists.</summary>
internal static class FamilyLinkPage
{
    public static async Task<Result<PagedList<FamilyLinkDto>>> ReadAsync(
        IQueryable<FamilyLink> links, Guid viewerUserId, int page, int pageSize,
        FamilyLinkDtoFactory dtos, CancellationToken ct)
    {
        var total = await links.CountAsync(ct).ConfigureAwait(false);
        var views = await links.AsNoTracking()
            .OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(FamilyLinkProjection.ToView)
            .ToListAsync(ct).ConfigureAwait(false);

        var items = await dtos.ToDtosAsync(views, viewerUserId, ct).ConfigureAwait(false);
        return Result.Success(new PagedList<FamilyLinkDto>(items, total, page, pageSize));
    }
}
