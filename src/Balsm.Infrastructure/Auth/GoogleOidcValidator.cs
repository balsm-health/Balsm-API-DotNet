using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Auth;

// Not sealed: ValidateAsync is virtual so handler tests can stand in for the
// provider without a real signed token.
public class GoogleOidcValidator(IConfiguration configuration, ILogger<GoogleOidcValidator> logger)
{
    private readonly string _clientId = configuration["Google:ClientId"]
        ?? throw new InvalidOperationException("Google:ClientId not configured");

    public virtual async Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_clientId]
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
