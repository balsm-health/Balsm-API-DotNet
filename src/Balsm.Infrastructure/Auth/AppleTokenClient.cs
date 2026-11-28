using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Balsm.Infrastructure.Auth;

/// <summary>
/// Sign in with Apple server calls: exchanges the authorization code for a
/// refresh token at sign-in, and revokes that token when the account is
/// deleted. Needs Apple:TeamId, Apple:ClientId (bundle id), Apple:KeyId and
/// Apple:PrivateKey (PKCS8 PEM). Never logs a code or a token.
/// </summary>
// Not sealed: members are virtual so handler tests can stand in for Apple.
public class AppleTokenClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<AppleTokenClient> logger)
{
    public const string HttpClientName = "apple";
    private const string TokenUrl = "https://appleid.apple.com/auth/token";
    private const string RevokeUrl = "https://appleid.apple.com/auth/revoke";

    private string? TeamId => configuration["Apple:TeamId"];
    private string? ClientId => configuration["Apple:ClientId"];
    private string? KeyId => configuration["Apple:KeyId"];
    private string? PrivateKey => configuration["Apple:PrivateKey"];

    public virtual bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TeamId) && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(KeyId) && !string.IsNullOrWhiteSpace(PrivateKey);

    /// Returns Apple's refresh token for <paramref name="authorizationCode"/>,
    /// or null on any failure (unconfigured, rejected, network, bad body).
    public virtual async Task<string?> ExchangeCodeAsync(string authorizationCode, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        try
        {
            using var form = new FormUrlEncodedContent([
                new("client_id", ClientId!),
                new("client_secret", BuildClientSecret(TeamId!, ClientId!, KeyId!, PrivateKey!)),
                new("code", authorizationCode),
                new("grant_type", "authorization_code"),
            ]);
            using var resp = await httpClientFactory.CreateClient(HttpClientName)
                .PostAsync(TokenUrl, form, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Apple code exchange rejected with status {StatusCode}", (int)resp.StatusCode);
                return null;
            }

            var body = await resp.Content.ReadFromJsonAsync<AppleTokenResponse>(ct).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(body?.RefreshToken) ? null : body.RefreshToken;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Apple code exchange failed: {ExceptionType}", ex.GetType().Name);
            return null;
        }
    }

    public virtual async Task<AppleRevokeOutcome> RevokeAsync(string refreshToken, CancellationToken ct)
    {
        if (!IsConfigured) return AppleRevokeOutcome.ConfigMissing;
        try
        {
            using var form = new FormUrlEncodedContent([
                new("client_id", ClientId!),
                new("client_secret", BuildClientSecret(TeamId!, ClientId!, KeyId!, PrivateKey!)),
                new("token", refreshToken),
                new("token_type_hint", "refresh_token"),
            ]);
            using var resp = await httpClientFactory.CreateClient(HttpClientName)
                .PostAsync(RevokeUrl, form, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode) return AppleRevokeOutcome.Revoked;

            logger.LogWarning("Apple revoke rejected with status {StatusCode}", (int)resp.StatusCode);
            return AppleRevokeOutcome.Failed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Apple revoke failed: {ExceptionType}", ex.GetType().Name);
            return AppleRevokeOutcome.Failed;
        }
    }

    private static string BuildClientSecret(string teamId, string clientId, string keyId, string privateKeyPem)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(
            privateKeyPem.Replace("-----BEGIN PRIVATE KEY-----", "")
                         .Replace("-----END PRIVATE KEY-----", "")
                         .Replace("\n", "").Replace("\r", "").Trim()), out _);

        var key = new ECDsaSecurityKey(ecdsa) { KeyId = keyId };
        var creds = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        var now = DateTimeOffset.UtcNow;
        var token = new JwtSecurityToken(
            issuer: teamId,
            audience: "https://appleid.apple.com",
            claims: [new Claim("sub", clientId)],
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(5).UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record AppleTokenResponse(
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
}

public enum AppleRevokeOutcome { Revoked, ConfigMissing, Failed }
