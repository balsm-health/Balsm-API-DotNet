using Balsm.SharedKernel.Results;

namespace Balsm.Auth.Domain;

/// <summary>Expected-failure catalog for the Auth context (Result pattern).</summary>
public static class AuthErrors
{
    /// <summary>Uniform credential failure — never reveals whether the
    /// account exists or the password is wrong.</summary>
    public static readonly Error InvalidCredentials =
        new("Auth.InvalidCredentials", "Invalid email or password.");
}
