using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Balsm.Infrastructure.Encryption;

/// <summary>
/// AES-256-GCM field-level encryption for date_of_birth_ciphertext.
/// Key sourced from DOB_ENCRYPTION_KEY env var (32 bytes, base64). FR-047/FR-048.
/// </summary>
public sealed class DobEncryptionService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key;
    private readonly ILogger<DobEncryptionService> _logger;

    public DobEncryptionService(IConfiguration configuration, ILogger<DobEncryptionService> logger)
    {
        _logger = logger;
        var keyBase64 = configuration["DobEncryption:Key"]
            ?? throw new InvalidOperationException("DobEncryption:Key not configured");
        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != 32)
            throw new InvalidOperationException("DobEncryption:Key must be 32 bytes (256-bit)");
    }

    // Returns nonce (12B) || tag (16B) || ciphertext
    public byte[] Encrypt(DateOnly dob)
    {
        var plaintext = Encoding.UTF8.GetBytes(dob.ToString("yyyy-MM-dd"));
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        ciphertext.CopyTo(result, NonceSize + TagSize);
        return result;
    }

    public DateOnly Decrypt(byte[] blob)
    {
        if (blob.Length < NonceSize + TagSize + 10)
            throw new CryptographicException("Invalid DOB ciphertext");

        var nonce = blob[..NonceSize];
        var tag = blob[NonceSize..(NonceSize + TagSize)];
        var ciphertext = blob[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return DateOnly.ParseExact(Encoding.UTF8.GetString(plaintext), "yyyy-MM-dd");
    }

    public bool IsUnderEighteen(byte[] ciphertext)
    {
        var dob = Decrypt(ciphertext);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Year;
        if (dob.AddYears(age) > today) age--;
        return age < 18;
    }

    // Generic string field-level encryption (same AES-256-GCM key + layout as
    // the DOB path). Used for national_id_ciphertext — another PHI/PII field
    // that must never be stored in plaintext (FR-047/FR-048).
    public byte[] EncryptString(string value)
    {
        var plaintext = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        ciphertext.CopyTo(result, NonceSize + TagSize);
        return result;
    }

    public string DecryptString(byte[] blob)
    {
        if (blob.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext");

        var nonce = blob[..NonceSize];
        var tag = blob[NonceSize..(NonceSize + TagSize)];
        var ciphertext = blob[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
