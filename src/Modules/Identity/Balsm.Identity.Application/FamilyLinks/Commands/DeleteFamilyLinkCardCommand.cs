using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Commands;

/// <summary>The owner stops sharing their card on this link: the ciphertext is purged.</summary>
public sealed record DeleteFamilyLinkCardCommand(
    Guid FamilyLinkId,
    Guid OwnerUserId,
    string? CorrelationId) : IRequest<Result>;
