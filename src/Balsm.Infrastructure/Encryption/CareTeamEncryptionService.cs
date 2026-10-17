using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Encryption;

/// <summary>
/// AES-256-GCM field-level encryption for care-team free-text columns (FR-502).
/// Key sourced from CareTeamEncryption:Key (32 bytes, base64) — deliberately a
/// DIFFERENT key from DobEncryption:Key so a care-team key rotation cannot
/// invalidate stored DOB ciphertext, and a compromise of one does not yield
/// the other.
/// </summary>
public sealed class CareTeamEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;
    private readonly ILogger<CareTeamEncryptionService> _logger;

    public CareTeamEncryptionService(IConfiguration configuration, ILogger<CareTeamEncryptionService> logger)
    {
        _logger = logger;
        var keyBase64 = configuration["CareTeamEncryption:Key"]
            ?? throw new InvalidOperationException("CareTeamEncryption:Key not configured");
        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
            throw new InvalidOperationException("CareTeamEncryption:Key must be 32 bytes (256-bit)");
    }

    // Returns nonce (12B) || tag (16B) || ciphertext
    public byte[] Encrypt(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[bytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, bytes, ciphertext, tag);

        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        ciphertext.CopyTo(result, NonceSize + TagSize);
        return result;
    }

    public string Decrypt(byte[] blob)
    {
        if (blob.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid care-team ciphertext");

        var nonce = blob[..NonceSize];
        var tag = blob[NonceSize..(NonceSize + TagSize)];
        var ciphertext = blob[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public byte[]? EncryptOptional(string? plaintext) =>
        string.IsNullOrEmpty(plaintext) ? null : Encrypt(plaintext);

    public string? DecryptOptional(byte[]? blob) =>
        blob is null || blob.Length == 0 ? null : Decrypt(blob);
}
