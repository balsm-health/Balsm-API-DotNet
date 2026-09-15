using Balsm.EmergencyQr.Domain.Events;
using Balsm.SharedKernel.Domain;

namespace Balsm.EmergencyQr.Domain.Entities;

public sealed class EmergencyQrToken : AggregateRoot
{
    /// <summary>ttl_seconds value that mints a permanent (never-expiring) token.</summary>
    public const int PermanentTtlSeconds = 0;

    /// <summary>The only token type in P001. Delegation tokens (P002) add values.</summary>
    public const string ProfileType = "profile";

    public static readonly int[] AllowedTtlSeconds = [PermanentTtlSeconds, 3600, 21600, 86400, 604800];

    public Guid UserId { get; private set; }
    public byte[] Ciphertext { get; private set; } = [];
    public string ProfileEtag { get; private set; } = string.Empty;

    /// <summary>Token type in the public resolve envelope (spec v2.0). Always
    /// "profile" in P001; the column exists so a future delegation token is a
    /// data change, not a schema change.</summary>
    public string Type { get; private set; } = ProfileType;

    public int TtlSeconds { get; private set; }

    /// <summary>Null for permanent tokens — they only stop resolving when revoked.</summary>
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private EmergencyQrToken() { }

    /// <param name="tokenId">Client-generated jti (offline-first mint: the app
    /// creates the QR before it can reach the server and syncs later). Must be
    /// a CSPRNG UUIDv4; null lets the database assign one.</param>
    public static EmergencyQrToken Mint(Guid userId, byte[] ciphertext, string profileEtag, int ttlSeconds, Guid? tokenId = null)
    {
        if (!AllowedTtlSeconds.Contains(ttlSeconds))
            throw new ArgumentException($"Invalid ttl: {ttlSeconds}. Allowed: {string.Join(",", AllowedTtlSeconds)}");
        if (tokenId == Guid.Empty)
            throw new ArgumentException("tokenId must not be the empty GUID");
        var token = new EmergencyQrToken
        {
            Id = tokenId ?? Guid.NewGuid(),
            UserId = userId,
            Ciphertext = ciphertext,
            ProfileEtag = profileEtag,
            Type = ProfileType,
            TtlSeconds = ttlSeconds,
            ExpiresAt = ttlSeconds == PermanentTtlSeconds ? null : DateTime.UtcNow.AddSeconds(ttlSeconds)
        };
        token.AddDomainEvent(new EmergencyQrTokenMinted(token.Id, userId, token.IsPermanent));
        return token;
    }

    /// <summary>
    /// Replaces the encrypted snapshot in place so the QR URL (jti + client-held
    /// key) stays stable while a scan always shows current data. The server only
    /// ever sees ciphertext; the AES key never leaves the device.
    /// </summary>
    public void UpdateCiphertext(byte[] ciphertext, string profileEtag)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot update a revoked or expired token");
        Ciphertext = ciphertext;
        ProfileEtag = profileEtag;
    }

    public void Revoke()
    {
        if (RevokedAt is not null) return; // idempotent — no duplicate event
        RevokedAt = DateTime.UtcNow;
        AddDomainEvent(new EmergencyQrTokenRevoked(Id, UserId));
    }
    /// <summary>Raises the scanned event for a successful public resolve —
    /// downstream reactions (scan push, audit) subscribe to this.</summary>
    public void RecordScan(string clientClass) => AddDomainEvent(new EmergencyQrTokenScanned(Id, UserId, clientClass));

    public bool IsPermanent => TtlSeconds == PermanentTtlSeconds;
    public bool IsActive => RevokedAt is null && (ExpiresAt is null || ExpiresAt > DateTime.UtcNow);
}
