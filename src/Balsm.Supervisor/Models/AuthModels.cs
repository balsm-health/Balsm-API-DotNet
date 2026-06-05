namespace Balsm.Supervisor.Models;

public sealed class SetupRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? WorkspaceName { get; set; }
    public string? WorkspaceSlug { get; set; }
    public string? Locale { get; set; }
}

public sealed class LoginRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}

public sealed class UseRecoveryCodeRequest
{
    public string RecoveryCode { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
