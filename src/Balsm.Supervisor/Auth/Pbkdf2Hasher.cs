using System.Security.Cryptography;

namespace Balsm.Supervisor.Auth;

public sealed class Pbkdf2Hasher : IPasswordHasher
{
    private const int Iterations = 100_000;
    private const int HashSize = 32;

    public string AlgorithmId => "pbkdf2";

    public byte[] Hash(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
    }

    public bool Verify(string password, byte[] salt, byte[] storedHash)
    {
        var computed = Hash(password, salt);
        return CryptographicOperations.FixedTimeEquals(computed, storedHash);
    }
}
