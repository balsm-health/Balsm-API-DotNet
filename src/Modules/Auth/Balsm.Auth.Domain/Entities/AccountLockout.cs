namespace Balsm.Auth.Domain.Entities;

public sealed class AccountLockout
{
    public string Identifier { get; private set; } = string.Empty;
    public string IdentifierType { get; private set; } = string.Empty;
    public short FailedAttempts { get; private set; }
    public DateTime RollingWindowStartedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? LockedUntil { get; private set; }

    private AccountLockout() { }

    public static AccountLockout Create(string identifier, string identifierType) =>
        new() { Identifier = identifier.ToLowerInvariant(), IdentifierType = identifierType };

    public void RecordFailure()
    {
        var now = DateTime.UtcNow;
        if (now - RollingWindowStartedAt > TimeSpan.FromMinutes(10))
        {
            FailedAttempts = 0;
            RollingWindowStartedAt = now;
        }
        FailedAttempts++;
        if (FailedAttempts >= 5)
            LockedUntil = now.AddMinutes(15);
    }

    public void RecordSuccess()
    {
        FailedAttempts = 0;
        LockedUntil = null;
    }

    public bool IsLocked => LockedUntil.HasValue && LockedUntil.Value > DateTime.UtcNow;
}
