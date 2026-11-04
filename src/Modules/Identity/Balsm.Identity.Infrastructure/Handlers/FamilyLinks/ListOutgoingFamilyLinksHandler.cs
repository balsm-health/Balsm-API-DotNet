using Balsm.Identity.Application.FamilyLinks;
using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.Identity.Application.FamilyLinks.Queries;
using Balsm.Identity.Infrastructure.Data;
using Balsm.Identity.Infrastructure.FamilyLinks;
using Balsm.SharedKernel.Pagination;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Infrastructure.Handlers.FamilyLinks;

/// <summary>Links the caller asked for — how the requester sees pending → linked.</summary>
public sealed class ListOutgoingFamilyLinksHandler(IdentityDbContext db, FamilyLinkDtoFactory dtos)
    : IRequestHandler<ListOutgoingFamilyLinksQuery, Result<PagedList<FamilyLinkDto>>>
{
    public Task<Result<PagedList<FamilyLinkDto>>> Handle(ListOutgoingFamilyLinksQuery query, CancellationToken ct)
    {
        FamilyLinkWire.TryParseStatus(query.Status, out var status);
        var links = db.FamilyLinks.Where(l => l.RequesterUserId == query.UserId);
        if (query.Status is not null) links = links.Where(l => l.Status == status);
        return FamilyLinkPage.ReadAsync(links, query.UserId, query.Page, query.PageSize, dtos, ct);
    }
}
