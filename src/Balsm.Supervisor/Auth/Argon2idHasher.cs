using System.Security.Cryptography;
using Konscious.Security.Cryptography;

namespace Balsm.Supervisor.Auth;

public sealed class Argon2idHasher : IPasswordHasher
{
    private const int MemorySize = 65_536;  // 64 MB
    private const int Iterations = 3;
    private const int Parallelism = 2;
    private const int SaltSize = 16;
    private const int TagSize = 32;

    public string AlgorithmId => "argon2id";

    public byte[] Hash(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = MemorySize,
            Iterations = Iterations,
            DegreeOfParallelism = Parallelism,
        };
        return argon2.GetBytes(TagSize);
    }

    public bool Verify(string password, byte[] salt, byte[] storedHash)
    {
        var computed = Hash(password, salt);
        return CryptographicOperations.FixedTimeEquals(computed, storedHash);
    }

    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);
}
