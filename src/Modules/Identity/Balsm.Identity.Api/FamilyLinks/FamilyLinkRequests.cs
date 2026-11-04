namespace Balsm.Identity.Api.FamilyLinks;

/// <summary>Body of <c>POST /api/v1/family-links</c>.</summary>
/// <param name="Id">Client-minted link id (UUID). Replaying the same id returns the existing link.</param>
/// <param name="Jti">The scanned profile-QR token id from <c>/t/{jti}#k=…</c>.</param>
/// <param name="Relation">How the requester relates to the person they add: spouse, partner, parent,
/// child, sibling, grandparent, relative, friend, guardian, caregiver, neighbour or other.</param>
/// <param name="PublicKey">Requester's device X25519 public key (base64, 32 bytes) for sealed card shares. Optional.</param>
public sealed record CreateFamilyLinkRequest(Guid Id, Guid Jti, string Relation, byte[]? PublicKey);

/// <summary>Optional body of <c>POST /api/v1/family-links/{id}/approve</c>.</summary>
/// <param name="PublicKey">Recipient's device X25519 public key (base64, 32 bytes). Optional.</param>
public sealed record ApproveFamilyLinkRequest(byte[]? PublicKey);

/// <summary>Body of <c>PUT /api/v1/family-links/{id}/keys</c>.</summary>
/// <param name="PublicKey">The caller's current device X25519 public key (base64, 32 bytes).</param>
public sealed record UpdateFamilyLinkKeyRequest(byte[] PublicKey);

/// <summary>Body of <c>PUT /api/v1/family-links/{id}/card</c> — the caller's emergency card sealed for the other party.</summary>
/// <param name="Version">Monotonic, starting at 1; must be newer than the stored version.</param>
/// <param name="EphemeralPublicKey">Ephemeral X25519 public key (base64, 32 bytes).</param>
/// <param name="Nonce">AES-GCM nonce (base64, 12 bytes).</param>
/// <param name="Ciphertext">AES-256-GCM ciphertext with tag (base64, at most 16 KB decoded).</param>
public sealed record UpsertFamilyLinkCardRequest(int Version, byte[] EphemeralPublicKey, byte[] Nonce, byte[] Ciphertext);
