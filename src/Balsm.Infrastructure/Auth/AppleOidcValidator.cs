using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Balsm.Infrastructure.Auth;

public sealed class AppleOidcValidator(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    HybridCache cache,
    ILogger<AppleOidcValidator> logger)
{
    private const string AppleKeysUrl = "https://appleid.apple.com/auth/keys";
    private const string AppleIssuer = "https://appleid.apple.com";
    private static readonly HybridCacheEntryOptions JwksCacheOptions = new() { Expiration = TimeSpan.FromHours(6) };

    private readonly string _clientId = configuration["Apple:ClientId"]
        ?? throw new InvalidOperationException("Apple:ClientId not configured");

    public async Task<AppleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct)
    {
        try
        {
            var keys = await GetAppleKeysAsync(ct);
            var handler = new JwtSecurityTokenHandler();

            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
                ValidateIssuer = true,
                ValidIssuer = AppleIssuer,
                ValidateAudience = true,
                ValidAudience = _clientId,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = handler.ValidateToken(idToken, parameters, out _);
            var sub = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value?.ToLowerInvariant();
            var emailVerified = principal.FindFirst("email_verified")?.Value == "true";
            var isPrivateEmail = principal.FindFirst("is_private_email")?.Value == "true";

            if (sub is null) return null;
            return new AppleTokenPayload(sub, email, emailVerified, isPrivateEmail);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Apple ID token validation failed");
            return null;
        }
    }

    private async Task<IEnumerable<JsonWebKey>> GetAppleKeysAsync(CancellationToken ct)
    {
        // Cache the raw JWKS JSON (serializes cleanly to any L2); parse per call — cheap.
        // A bad payload throws inside the factory, so nothing is cached and the next
        // request refetches instead of breaking Apple sign-in for the whole window.
        var json = await cache.GetOrCreateAsync(
            "apple_jwks",
            async token =>
            {
                var http = httpClientFactory.CreateClient();
                var body = await http.GetStringAsync(AppleKeysUrl, token).ConfigureAwait(false);
                return new JsonWebKeySet(body).Keys.Count > 0
                    ? body
                    : throw new InvalidOperationException("Apple JWKS returned no keys");
            },
            JwksCacheOptions,
            cancellationToken: ct).ConfigureAwait(false);

        return new JsonWebKeySet(json).Keys;
    }
}

public sealed record AppleTokenPayload(string Subject, string? Email, bool EmailVerified, bool IsPrivateEmail);
