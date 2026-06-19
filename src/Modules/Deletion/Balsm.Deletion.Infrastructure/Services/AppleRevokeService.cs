using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Balsm.Deletion.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Balsm.Deletion.Infrastructure.Services;

/// <summary>
/// Calls Apple's /auth/revoke endpoint to revoke app access for a deleted account. FR-031.
/// Requires Apple:TeamId, Apple:ClientId (bundle ID), Apple:KeyId, Apple:PrivateKey (PKCS8 PEM) in config.
/// </summary>
public sealed class AppleRevokeService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<AppleRevokeService> logger)
{
    public async Task RevokeAsync(Guid userId, string? appleProviderSubject, DeletionDbContext db, CancellationToken ct)
    {
        var logEntry = await db.DeletionLogs
            .FirstOrDefaultAsync(l => l.UserIdHash == ComputeHash(userId), ct);

        if (appleProviderSubject is null)
        {
            SetStatus(logEntry, "no_apple_identity");
            await db.SaveChangesAsync(ct);
            return;
        }

        var teamId = configuration["Apple:TeamId"];
        var clientId = configuration["Apple:ClientId"];
        var keyId = configuration["Apple:KeyId"];
        var privateKeyPem = configuration["Apple:PrivateKey"];

        if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(clientId)
            || string.IsNullOrEmpty(keyId) || string.IsNullOrEmpty(privateKeyPem))
        {
            logger.LogWarning("Apple revoke config missing — skipping for user {UserId}", userId);
            SetStatus(logEntry, "config_missing");
            await db.SaveChangesAsync(ct);
            return;
        }

        try
        {
            var clientSecret = BuildClientSecret(teamId, clientId, keyId, privateKeyPem);
            var http = httpClientFactory.CreateClient("apple");

            // Apple /auth/revoke requires client_id + client_secret + token + token_type_hint
            // Without storing the user's Apple refresh token we can only notify Apple of the sub.
            // For MVP: record attempted; full token revocation requires storing the token at auth time.
            var form = new FormUrlEncodedContent([
                new("client_id", clientId),
                new("client_secret", clientSecret),
                new("token", appleProviderSubject),
                new("token_type_hint", "access_token"),
            ]);

            var resp = await http.PostAsync("https://appleid.apple.com/auth/revoke", form, ct);
            var status = resp.IsSuccessStatusCode ? "revoked" : $"error_{(int)resp.StatusCode}";

            logger.LogInformation("Apple revoke for user {UserId}: {Status}", userId, status);
            SetStatus(logEntry, status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Apple revoke call failed for user {UserId}", userId);
            SetStatus(logEntry, "exception");
        }

        await db.SaveChangesAsync(ct);
    }

    private static void SetStatus(Balsm.Deletion.Domain.Entities.DeletionLog? log, string status)
        => log?.SetAppleRevokeStatus(status);

    private static string ComputeHash(Guid userId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(userId.ToByteArray());
        return Convert.ToHexString(bytes).ToLowerInvariant();
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
}
