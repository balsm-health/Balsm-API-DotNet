using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Commands;

/// <summary>The recipient approves a pending request (pending → linked).</summary>
/// <param name="PublicKey">Recipient's device X25519 public key (32 bytes), optional.</param>
public sealed record ApproveFamilyLinkCommand(
    Guid FamilyLinkId,
    Guid UserId,
    byte[]? PublicKey,
    string? CorrelationId) : IRequest<Result<FamilyLinkDto>>;
