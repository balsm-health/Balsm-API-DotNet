using Balsm.SharedKernel.Results;
using MediatR;

namespace Balsm.Identity.Application.FamilyLinks.Commands;

/// <summary>
/// The owner stores a newer version of their emergency card, sealed for the
/// other party's device key. The server never sees the card in readable form.
/// </summary>
/// <param name="OwnerUserId">From the token's sub claim.</param>
public sealed record UpsertFamilyLinkCardCommand(
    Guid FamilyLinkId,
    Guid OwnerUserId,
    int Version,
    byte[] EphemeralPublicKey,
    byte[] Nonce,
    byte[] Ciphertext,
    string? CorrelationId) : IRequest<Result>;
