namespace Balsm.Account.Domain.Entities;

public sealed class UsernameReservation
{
    public string HandleNormalized { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public DateTime ClaimedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ReleasedAt { get; private set; }

    private UsernameReservation() { }

    public static UsernameReservation Create(string handle, Guid userId) =>
        new() { HandleNormalized = handle.ToLowerInvariant(), UserId = userId };

    public void Release() => ReleasedAt = DateTime.UtcNow;
    public bool IsActive => ReleasedAt is null;
}
