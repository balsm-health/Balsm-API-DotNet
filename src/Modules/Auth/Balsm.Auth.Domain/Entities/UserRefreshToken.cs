namespace Balsm.Auth.Domain.Entities;

public sealed class UserRefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid DeviceId { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private UserRefreshToken() { }

    public static UserRefreshToken Create(Guid userId, string tokenHash, Guid deviceId, DateTime expiresAt) =>
        new() { UserId = userId, TokenHash = tokenHash, DeviceId = deviceId, ExpiresAt = expiresAt };

    public void Revoke() => RevokedAt = DateTime.UtcNow;
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
