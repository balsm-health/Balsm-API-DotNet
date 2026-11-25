using Balsm.Identity.Application.FamilyLinks.Dtos;
using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Commands;

/// <summary>
/// The requester asks to link with the owner of a scanned profile QR.
/// Idempotent on <paramref name="Id"/>: a replay returns the existing link.
/// </summary>
/// <param name="Id">Client-minted link id.</param>
/// <param name="RequesterUserId">From the token's sub claim — never the body.</param>
/// <param name="Jti">The scanned profile-QR token id.</param>
/// <param name="Relation">App <c>Relationship</c> wire code.</param>
/// <param name="PublicKey">Requester's device X25519 public key (32 bytes), optional.</param>
public sealed record CreateFamilyLinkCommand(
    Guid Id,
    Guid RequesterUserId,
    Guid Jti,
    string Relation,
    byte[]? PublicKey,
    string? CorrelationId) : IRequest<Result<CreateFamilyLinkResult>>;
