namespace Balsm.Sessions.Domain.Entities;

public sealed class ActiveSession
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string DeviceLabel { get; private set; } = string.Empty;
    public string DeviceType { get; private set; } = "phone";
    public DateTime FirstSeenAt { get; private set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; private set; }
    public Guid RefreshTokenId { get; private set; }

    private ActiveSession() { }

    public static ActiveSession Create(Guid userId, Guid deviceId, string deviceLabel, string deviceType, Guid refreshTokenId) =>
        new() { UserId = userId, DeviceId = deviceId, DeviceLabel = deviceLabel, DeviceType = deviceType, RefreshTokenId = refreshTokenId };

    public void Revoke() => RevokedAt = DateTime.UtcNow;
    public void Touch() => LastActivityAt = DateTime.UtcNow;
    public bool IsActive => RevokedAt is null;
}
