using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Balsm.Infrastructure.Auth;

/// Argon2id password hasher for patient credentials. Mirrors the Supervisor's
/// admin hasher parameters. Produces and verifies a self-describing PHC-style
/// string so salt + parameters travel with the hash.
public sealed class PasswordHasher
{
    private const int MemorySize = 65_536; // 64 MB
    private const int Iterations = 3;
    private const int Parallelism = 2;
    private const int SaltSize = 16;
    private const int TagSize = 32;

    /// Hash [password] into a self-contained encoding:
    /// `argon2id$<salt-b64>$<tag-b64>`.
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var tag = Derive(password, salt);
        return $"argon2id${Convert.ToBase64String(salt)}${Convert.ToBase64String(tag)}";
    }

    /// Constant-time verify of [password] against an encoded hash from [Hash].
    /// Returns false on any malformed encoding rather than throwing.
    public bool Verify(string password, string encoded)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 3 || parts[0] != "argon2id") return false;
        byte[] salt, tag;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            tag = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }
        var computed = Derive(password, salt);
        return CryptographicOperations.FixedTimeEquals(computed, tag);
    }

    private static byte[] Derive(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemorySize,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism,
        };
        return argon2.GetBytes(TagSize);
    }
}
