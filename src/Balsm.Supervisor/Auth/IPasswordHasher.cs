namespace Balsm.Supervisor.Auth;

public interface IPasswordHasher
{
    string AlgorithmId { get; }
    byte[] Hash(string password, byte[] salt);
    bool Verify(string password, byte[] salt, byte[] storedHash);
}
