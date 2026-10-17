using Balsm.SharedKernel.Domain;

namespace Balsm.Identity.Domain;

public sealed class AdminUserMirror : AggregateRoot
{
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Role { get; private set; } = "SystemAdmin";
    public string Locale { get; private set; } = "en";
    public DateTime? LastLoginAt { get; private set; }
    public DateTime PasswordChangedAt { get; private set; }

    private AdminUserMirror() { }

    public static AdminUserMirror Create(string email, string displayName, string locale = "en")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new AdminUserMirror
        {
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            Locale = locale,
            PasswordChangedAt = DateTime.UtcNow,
        };
    }

    public void UpdateProfile(string displayName, string locale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
        Locale = locale;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void RecordPasswordChange()
    {
        PasswordChangedAt = DateTime.UtcNow;
    }
}
