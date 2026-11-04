using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Commands;

/// <summary>Either party uploads its current device X25519 public key for this link.</summary>
public sealed record UpdateFamilyLinkKeyCommand(
    Guid FamilyLinkId,
    Guid UserId,
    byte[] PublicKey,
    string? CorrelationId) : IRequest<Result>;
