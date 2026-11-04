using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Queries;

/// <summary>One link, visible only to its two parties.</summary>
public sealed record GetFamilyLinkByIdQuery(Guid FamilyLinkId, Guid UserId) : IRequest<Result<FamilyLinkDto>>;
