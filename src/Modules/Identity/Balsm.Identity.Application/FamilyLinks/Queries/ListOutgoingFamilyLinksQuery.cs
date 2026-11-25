using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.SharedKernel.Pagination;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Queries;

/// <summary>Links where the caller is the requester, newest first.</summary>
/// <param name="Status">Optional status filter; null lists every status.</param>
public sealed record ListOutgoingFamilyLinksQuery(Guid UserId, string? Status, int Page, int PageSize)
    : IRequest<Result<PagedList<FamilyLinkDto>>>;
