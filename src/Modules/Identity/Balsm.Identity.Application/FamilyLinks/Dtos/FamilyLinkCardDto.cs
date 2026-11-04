namespace Balsm.Identity.Application.FamilyLinks.Dtos;

/// <summary>
/// The other party's emergency card, sealed for the caller's device key. Opaque
/// to the server; the app opens it with ECIES (X25519 + HKDF-SHA256, info
/// <c>balsm-family-card-v1</c>, salt = link id) and AES-256-GCM with AAD
/// <c>link id | owner user id | version</c>.
/// </summary>
/// <param name="FamilyLinkId">The link the card was sealed for.</param>
/// <param name="OwnerUserId">The account that sealed and shares the card.</param>
/// <param name="Version">Monotonic version set by the owner's app.</param>
/// <param name="EphemeralPublicKey">Sender's ephemeral X25519 public key (base64, 32 bytes).</param>
/// <param name="Nonce">AES-GCM nonce (base64, 12 bytes).</param>
/// <param name="Ciphertext">AES-GCM ciphertext with tag (base64, at most 16 KB).</param>
/// <param name="UpdatedAt">When this version was uploaded.</param>
public sealed record FamilyLinkCardDto(
    Guid FamilyLinkId,
    Guid OwnerUserId,
    int Version,
    byte[] EphemeralPublicKey,
    byte[] Nonce,
    byte[] Ciphertext,
    DateTime UpdatedAt);
