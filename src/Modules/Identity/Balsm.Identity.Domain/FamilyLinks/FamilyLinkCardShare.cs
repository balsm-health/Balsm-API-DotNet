namespace Balsm.Identity.Domain.FamilyLinks;

/// <summary>
/// One owner's emergency card, sealed end-to-end for the other party's device
/// key (X25519 + HKDF-SHA256 + AES-256-GCM). The server holds only the sealed
/// envelope and cannot open it. Purged — really deleted, not soft-deleted — when
/// the owner stops sharing or the link ends: it is the owner's PHI copy, not a
/// clinical record. The audit trail of the share stays.
/// </summary>
public sealed class FamilyLinkCardShare
{
    /// <summary>Upper bound on the sealed ciphertext, GCM tag included.</summary>
    public const int MaxCiphertextBytes = 16 * 1024;

    /// <summary>X25519 public key length.</summary>
    public const int PublicKeyBytes = 32;

    /// <summary>AES-GCM nonce length.</summary>
    public const int NonceBytes = 12;

    public Guid Id { get; private set; }
    public Guid FamilyLinkId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    /// <summary>Owner-chosen, strictly increasing (the app uses its clock in ms).</summary>
    public long Version { get; private set; }
    public byte[] EphemeralPublicKey { get; private set; } = [];
    public byte[] Nonce { get; private set; } = [];
    public byte[] Ciphertext { get; private set; } = [];
    public DateTime UpdatedAt { get; private set; }

    private FamilyLinkCardShare() { }

    internal static FamilyLinkCardShare Create(Guid linkId, Guid ownerUserId) => new()
    {
        Id = Guid.NewGuid(),
        FamilyLinkId = linkId,
        OwnerUserId = ownerUserId,
    };

    internal bool IsSameEnvelope(long version, byte[] ephemeralPublicKey, byte[] nonce, byte[] ciphertext) =>
        Version == version
        && EphemeralPublicKey.AsSpan().SequenceEqual(ephemeralPublicKey)
        && Nonce.AsSpan().SequenceEqual(nonce)
        && Ciphertext.AsSpan().SequenceEqual(ciphertext);

    internal void Replace(long version, byte[] ephemeralPublicKey, byte[] nonce, byte[] ciphertext, DateTime now)
    {
        Version = version;
        EphemeralPublicKey = ephemeralPublicKey;
        Nonce = nonce;
        Ciphertext = ciphertext;
        UpdatedAt = now;
    }
}
