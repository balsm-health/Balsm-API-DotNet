namespace Balsam.Supervisor.Auth;

public interface ICredentialStore
{
    Task<bool> HasCredentialsAsync(CancellationToken ct = default);
    Task SaveCredentialsAsync(AdminCredentials credentials, CancellationToken ct = default);
    Task<AdminCredentials?> LoadCredentialsAsync(CancellationToken ct = default);
    Task UpdatePasswordAsync(string passwordHash, string salt, CancellationToken ct = default);
}
