namespace Balsm.Supervisor.Auth;

public sealed class AdminCredentials
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastPasswordChange { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }

    // Phase 0 additive fields — backward-compatible; defaults applied on read
    public string PasswordHashAlgorithm { get; set; } = "pbkdf2";
    public string? RecoveryCodeHash { get; set; }
    public DateTime? RecoveryCodeCreatedAt { get; set; }
    public DateTime? RecoveryCodeUsedAt { get; set; }
    public DateTime? RecoveryCodeRetiredAt { get; set; }

    // Locale preference per admin user (FR-019); defaults to "en" for backward compat
    public string Locale { get; set; } = "en";
}
