using Balsm.SharedKernel.Results;

namespace Balsm.Auth.Domain;

/// <summary>Expected-failure catalog for the Auth context (Result pattern).</summary>
public static class AuthErrors
{
    /// <summary>Uniform credential failure — never reveals whether the
    /// account exists or the password is wrong.</summary>
    public static readonly Error InvalidCredentials =
        new("Auth.InvalidCredentials", "Invalid email or password.");

    /// <summary>The message sent with a 423 AccountLocked: too many failed
    /// attempts for an address, unknown addresses included.</summary>
    public static string LockedMessage(int seconds) => $"Too many attempts. Try again in {seconds} seconds.";
}
