using Balsm.SharedKernel.Domain;

namespace Balsm.EmergencyQr.Domain.Entities;

public sealed class EmergencyQrToken : AggregateRoot
{
    public static readonly int[] AllowedTtlSeconds = [3600, 21600, 86400, 604800];

    public Guid UserId { get; private set; }
    public byte[] Ciphertext { get; private set; } = [];
    public string ProfileEtag { get; private set; } = string.Empty;
    public string PreferredLanguage { get; private set; } = "en";
    public int TtlSeconds { get; private set; }
    public DateTime ExpiresAt { get; private set; }
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
            ExpiresAt = DateTime.UtcNow.AddSeconds(ttlSeconds)
        };
    }

    public void Revoke() => RevokedAt = DateTime.UtcNow;
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
