using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Balsm.Infrastructure.Auth;

public sealed class AppleOidcValidator(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    ILogger<AppleOidcValidator> logger)
{
    private const string AppleKeysUrl = "https://appleid.apple.com/auth/keys";
    private const string AppleIssuer = "https://appleid.apple.com";
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
        if (cache.TryGetValue("apple_jwks", out IEnumerable<JsonWebKey>? cached) && cached is not null)
            return cached;

        var http = httpClientFactory.CreateClient();
        var jwks = await http.GetFromJsonAsync<AppleJwks>(AppleKeysUrl, ct)
            ?? throw new InvalidOperationException("Failed to fetch Apple JWKS");

        var keys = jwks.Keys.Select(k => new JsonWebKey(System.Text.Json.JsonSerializer.Serialize(k)));
        cache.Set("apple_jwks", keys, TimeSpan.FromHours(6));
        return keys;
    }

    private sealed record AppleJwks(List<System.Text.Json.JsonElement> Keys);
}

public sealed record AppleTokenPayload(string Subject, string? Email, bool EmailVerified, bool IsPrivateEmail);
