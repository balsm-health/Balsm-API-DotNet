using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Balsm.Infrastructure.Encryption;

/// <summary>
/// AES-256-GCM encryption for the Apple refresh token kept on a user identity
/// so account deletion can revoke it. Layout matches
/// <see cref="DobEncryptionService"/>: nonce (12B) || tag (16B) || ciphertext.
/// The key is derived from <c>DobEncryption:Key</c> with HKDF under its own
/// info label, so a DOB ciphertext never decrypts here and vice versa.
/// The key is checked on first use, not at construction: a deployment
/// without it must still be able to sign users in with Apple.
/// </summary>
public sealed class AppleRefreshTokenCipher(IConfiguration configuration)
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static readonly byte[] KeyInfo = Encoding.UTF8.GetBytes("balsm/apple-refresh-token/v1");

    private readonly Lazy<byte[]> _key = new(() => DeriveKey(configuration));

    public byte[] Encrypt(string token)
    {
        var plaintext = Encoding.UTF8.GetBytes(token);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key.Value, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        ciphertext.CopyTo(result, NonceSize + TagSize);
        return result;
    }

    public string Decrypt(byte[] blob)
    {
        if (blob.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext");

        var nonce = blob[..NonceSize];
        var tag = blob[NonceSize..(NonceSize + TagSize)];
        var ciphertext = blob[(NonceSize + TagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(_key.Value, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] DeriveKey(IConfiguration configuration)
    {
        var keyBase64 = configuration["DobEncryption:Key"]
            ?? throw new InvalidOperationException("DobEncryption:Key not configured");
        var dobKey = Convert.FromBase64String(keyBase64);
        if (dobKey.Length != 32)
            throw new InvalidOperationException("DobEncryption:Key must be 32 bytes (256-bit)");
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, dobKey, 32, info: KeyInfo);
    }
}
