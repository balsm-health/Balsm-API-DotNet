using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Balsm.Infrastructure.Auth;

// Not sealed: ValidateAsync is virtual so handler tests can stand in for the
// provider without a real signed token, and GetSigningKeysAsync is virtual so
// endpoint tests can sign with their own key instead of fetching Apple's JWKS.
public class AppleOidcValidator(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    HybridCache cache,
    ILogger<AppleOidcValidator> logger)
{
    private const string AppleKeysUrl = "https://appleid.apple.com/auth/keys";
    private const string AppleIssuer = "https://appleid.apple.com";
    // Apple signs ID tokens and notifications with RS256 only; pinning it keeps
    // any other algorithm (and any non-RSA key that might appear) out.
    private static readonly string[] AppleAlgorithms = [SecurityAlgorithms.RsaSha256];
    // Apple retries a notification for up to 7 days; anything older is a replay.
    private static readonly TimeSpan NotificationMaxAge = TimeSpan.FromDays(7);
    private static readonly TimeSpan NotificationClockSkew = TimeSpan.FromMinutes(5);
    private static readonly HybridCacheEntryOptions JwksCacheOptions = new() { Expiration = TimeSpan.FromHours(6) };

    // May be missing while SocialSignIn:Enabled is false (the boot check
    // requires it only when enabled); callers check IsConfigured.
    private readonly string? _clientId = configuration["Apple:ClientId"];

    /// <summary>False when <c>Apple:ClientId</c> is not set: no token can be validated.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId);

    public virtual async Task<AppleTokenPayload?> ValidateAsync(string idToken, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        try
        {
            var keys = await GetSigningKeysAsync(ct);
            // Keep claim names as Apple sends them: the default mapping renames
            // `sub` to the NameIdentifier URI, which made every token read as subject-less.
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = keys,
                ValidAlgorithms = AppleAlgorithms,
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
            // JSON boolean or "true"/"false" string, in any case.
            var emailVerified = IsTrue(principal.FindFirst("email_verified")?.Value);
            var isPrivateEmail = IsTrue(principal.FindFirst("is_private_email")?.Value);
            var nonce = principal.FindFirst("nonce")?.Value;

            if (sub is null) return null;
            return new AppleTokenPayload(sub, email, emailVerified, isPrivateEmail, nonce);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Apple ID token validation failed");
            return null;
        }
    }

    private static bool IsTrue(string? claim) => bool.TryParse(claim, out var value) && value;

    /// <summary>
    /// Validates the <c>payload</c> JWT of an Apple server-to-server notification
    /// against the same keys, issuer and audience as an ID token, and parses its
    /// <c>events</c> claim. Null when the token or its event is invalid. Neither the
    /// token nor anything in it is logged.
    /// </summary>
    public async Task<AppleServerEvent?> ValidateServerNotificationAsync(string payloadJwt, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        try
        {
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = await GetSigningKeysAsync(ct).ConfigureAwait(false),
                ValidAlgorithms = AppleAlgorithms,
                ValidateIssuer = true,
                // Apple's documentation shows the issuer both with and without a
                // trailing slash; both name the same origin.
                ValidIssuers = [AppleIssuer, AppleIssuer + "/"],
                ValidateAudience = true,
                ValidAudience = _clientId,
                ValidateLifetime = true,
                // Notification tokens may carry only iat; an exp, when present, is enforced.
                RequireExpirationTime = false,
                ClockSkew = NotificationClockSkew
            };

            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }
                .ValidateToken(payloadJwt, parameters, out _);
            if (!IssuedWithinRetryWindow(principal.FindFirst("iat")?.Value))
            {
                logger.LogWarning("Apple server notification rejected: iat missing or outside the retry window");
                return null;
            }
            var events = principal.FindFirst("events")?.Value;
            return events is null ? null : ParseEvent(events);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Apple server notification validation failed: {ExceptionType}", ex.GetType().Name);
            return null;
        }
    }

    // The notification token has no exp, so its iat is what bounds a replay:
    // required, at most 7 days old, and not meaningfully in the future.
    private static bool IssuedWithinRetryWindow(string? iat)
    {
        if (!long.TryParse(iat, out var seconds)) return false;
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
        var now = DateTimeOffset.UtcNow;
        return issuedAt >= now - NotificationMaxAge && issuedAt <= now + NotificationClockSkew;
    }

    protected virtual async Task<IEnumerable<SecurityKey>> GetSigningKeysAsync(CancellationToken ct) =>
        await GetAppleKeysAsync(ct).ConfigureAwait(false);

    // `events` is a JSON object serialized into a string claim. Apple sends
    // is_private_email as "true"/"false" and event_time in epoch milliseconds;
    // both are read leniently (bool or string; seconds or milliseconds).
    private static AppleServerEvent? ParseEvent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = String(root, "type");
        var sub = String(root, "sub");
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(sub)) return null;

        var eventTime = DateTimeOffset.UnixEpoch;
        if (root.TryGetProperty("event_time", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt64(out var raw))
            eventTime = raw > 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(raw)
                : DateTimeOffset.FromUnixTimeSeconds(raw);

        bool? isPrivate = root.TryGetProperty("is_private_email", out var p)
            ? p.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(p.GetString(), out var b) ? b : null,
                _ => null
            }
            : null;

        return new AppleServerEvent(type, sub, eventTime, String(root, "email"), isPrivate);
    }

    private static string? String(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

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

/// <param name="Nonce">The <c>nonce</c> claim: the SHA-256 hex of the raw nonce the app generated.</param>
public sealed record AppleTokenPayload(
    string Subject, string? Email, bool EmailVerified, bool IsPrivateEmail, string? Nonce);

/// <summary>One event from an Apple server-to-server notification.</summary>
/// <param name="Type"><c>consent-revoked</c>, <c>account-delete</c>, <c>email-disabled</c>, <c>email-enabled</c>, or a type Apple adds later.</param>
/// <param name="Subject">The Apple <c>sub</c> the event is about. Never logged.</param>
/// <param name="Email">The (relay) address, sent with the email events. Never logged.</param>
public sealed record AppleServerEvent(
    string Type, string Subject, DateTimeOffset EventTime, string? Email, bool? IsPrivateEmail);
