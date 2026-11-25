using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.SharedKernel.Pagination;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Queries;

/// <summary>Links where the caller is the recipient, newest first.</summary>
/// <param name="Status">Optional status filter (e.g. <c>pending</c>); null lists every status.</param>
public sealed record ListIncomingFamilyLinksQuery(Guid UserId, string? Status, int Page, int PageSize)
    : IRequest<Result<PagedList<FamilyLinkDto>>>;
