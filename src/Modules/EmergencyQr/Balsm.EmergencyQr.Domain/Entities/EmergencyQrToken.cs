using Balsm.SharedKernel.Domain;

namespace Balsm.EmergencyQr.Domain.Entities;

public sealed class EmergencyQrToken : AggregateRoot
{
    /// <summary>ttl_seconds value that mints a permanent (never-expiring) token.</summary>
    public const int PermanentTtlSeconds = 0;

    public static readonly int[] AllowedTtlSeconds = [PermanentTtlSeconds, 3600, 21600, 86400, 604800];

    public Guid UserId { get; private set; }
    public byte[] Ciphertext { get; private set; } = [];
    public string ProfileEtag { get; private set; } = string.Empty;
    public string PreferredLanguage { get; private set; } = "en";
    public int TtlSeconds { get; private set; }

    /// <summary>Null for permanent tokens — they only stop resolving when revoked.</summary>
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private EmergencyQrToken() { }

    public static EmergencyQrToken Mint(Guid userId, byte[] ciphertext, string profileEtag, string preferredLanguage, int ttlSeconds)
    {
        if (!AllowedTtlSeconds.Contains(ttlSeconds))
            throw new ArgumentException($"Invalid ttl: {ttlSeconds}. Allowed: {string.Join(",", AllowedTtlSeconds)}");
        return new EmergencyQrToken
        {
            UserId = userId,
            Ciphertext = ciphertext,
            ProfileEtag = profileEtag,
            PreferredLanguage = preferredLanguage,
            TtlSeconds = ttlSeconds,
            ExpiresAt = ttlSeconds == PermanentTtlSeconds ? null : DateTime.UtcNow.AddSeconds(ttlSeconds)
        };
    }

    /// <summary>
    /// Replaces the encrypted snapshot in place so the QR URL (jti + client-held
    /// key) stays stable while a scan always shows current data. The server only
    /// ever sees ciphertext; the AES key never leaves the device.
    /// </summary>
    public void UpdateCiphertext(byte[] ciphertext, string profileEtag, string preferredLanguage)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot update a revoked or expired token");
        Ciphertext = ciphertext;
        ProfileEtag = profileEtag;
        PreferredLanguage = preferredLanguage;
    }

    public void Revoke() => RevokedAt = DateTime.UtcNow;
    public bool IsPermanent => TtlSeconds == PermanentTtlSeconds;
    public bool IsActive => RevokedAt is null && (ExpiresAt is null || ExpiresAt > DateTime.UtcNow);
}
