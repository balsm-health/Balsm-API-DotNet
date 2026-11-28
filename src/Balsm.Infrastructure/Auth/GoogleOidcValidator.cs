using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Auth;

// Not sealed: ValidateAsync is virtual so handler tests can stand in for the
// provider without a real signed token.
public class GoogleOidcValidator(IConfiguration configuration, ILogger<GoogleOidcValidator> logger)
{
    // May be missing while SocialSignIn:Enabled is false (the boot check
    // requires it only when enabled); callers check IsConfigured.
    private readonly string? _clientId = configuration["Google:ClientId"];

    /// <summary>False when <c>Google:ClientId</c> is not set: no token can be validated.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId);

    public virtual async Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_clientId!]
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            return new GoogleTokenPayload(
                payload.Subject,
                payload.Email?.ToLowerInvariant(),
                payload.EmailVerified);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Google ID token validation failed");
            return null;
        }
    }
}

public sealed record GoogleTokenPayload(string Subject, string? Email, bool EmailVerified);
