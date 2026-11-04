using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Queries;

/// <summary>The newest card the other party shares with the caller on a linked link. Every read is audited.</summary>
public sealed record GetFamilyLinkCardQuery(Guid FamilyLinkId, Guid ViewerUserId, string? CorrelationId)
    : IRequest<Result<FamilyLinkCardDto>>;
