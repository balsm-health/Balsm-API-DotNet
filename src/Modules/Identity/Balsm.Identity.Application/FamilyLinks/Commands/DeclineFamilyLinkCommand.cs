using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Commands;

/// <summary>The recipient declines a pending request (pending → declined).</summary>
public sealed record DeclineFamilyLinkCommand(
    Guid FamilyLinkId,
    Guid UserId,
    string? CorrelationId) : IRequest<Result<FamilyLinkDto>>;
