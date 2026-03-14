namespace Balsam.Supervisor.Auth;

public sealed class LoginResult
{
    public bool IsSuccess { get; private init; }
    public bool IsLockedOut { get; private init; }
    public string? ErrorMessage { get; private init; }
    public TimeSpan? LockoutRemaining { get; private init; }

    public static LoginResult Success() => new() { IsSuccess = true };

    public static LoginResult Failure(string message, bool lockedOut = false)
        => new() { ErrorMessage = message, IsLockedOut = lockedOut };

    public static LoginResult LockedOut(TimeSpan remaining)
        => new()
        {
            IsLockedOut = true,
            LockoutRemaining = remaining,
            ErrorMessage = $"Account locked. Try again in {(int)remaining.TotalMinutes} minutes."
        };
}
