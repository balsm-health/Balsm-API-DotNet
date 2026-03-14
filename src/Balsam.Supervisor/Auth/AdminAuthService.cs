using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Balsam.Supervisor.Auth;

public sealed class AdminAuthService
{
    private const int Pbkdf2Iterations = 100_000;
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(1);

    private readonly ICredentialStore _store;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(ICredentialStore store, ILogger<AdminAuthService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<bool> IsSetupCompleteAsync(CancellationToken ct = default)
        => await _store.HasCredentialsAsync(ct);

    public async Task SetupAsync(string username, string password, CancellationToken ct = default)
    {
        if (await _store.HasCredentialsAsync(ct))
            throw new InvalidOperationException("Setup already completed");

        if (password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = HashPassword(password, salt);

        var creds = new AdminCredentials
        {
            Username = username,
            PasswordHash = Convert.ToBase64String(hash),
            Salt = Convert.ToBase64String(salt),
            CreatedAt = DateTime.UtcNow
        };

        await _store.SaveCredentialsAsync(creds, ct);
        _logger.LogInformation("Admin credentials created for user {Username}", username);
    }

    public async Task<LoginResult> LoginAsync(
        string username, string password, CancellationToken ct = default)
    {
        var creds = await _store.LoadCredentialsAsync(ct);
        if (creds is null) return LoginResult.Failure("Setup not completed");

        if (creds.LockoutEnd.HasValue && creds.LockoutEnd > DateTime.UtcNow)
        {
            var remaining = creds.LockoutEnd.Value - DateTime.UtcNow;
            return LoginResult.LockedOut(remaining);
        }

        var salt = Convert.FromBase64String(creds.Salt);
        var expectedHash = Convert.FromBase64String(creds.PasswordHash);
        var actualHash = HashPassword(password, salt);

        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash)
            || !string.Equals(username, creds.Username, StringComparison.Ordinal))
        {
            creds.FailedLoginAttempts++;

            if (creds.FailedLoginAttempts >= MaxFailedAttempts)
            {
                creds.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                _logger.LogWarning(
                    "Admin account locked out until {LockoutEnd}", creds.LockoutEnd);
            }

            await _store.SaveCredentialsAsync(creds, ct);
            await Task.Delay(FailureDelay, ct);

            return creds.FailedLoginAttempts >= MaxFailedAttempts
                ? LoginResult.LockedOut(LockoutDuration)
                : LoginResult.Failure("Invalid credentials");
        }

        creds.FailedLoginAttempts = 0;
        creds.LockoutEnd = null;
        await _store.SaveCredentialsAsync(creds, ct);

        _logger.LogInformation("Admin login successful for {Username}", username);
        return LoginResult.Success();
    }

    public async Task ChangePasswordAsync(
        string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var creds = await _store.LoadCredentialsAsync(ct)
            ?? throw new InvalidOperationException("No credentials configured");

        var salt = Convert.FromBase64String(creds.Salt);
        var expectedHash = Convert.FromBase64String(creds.PasswordHash);
        var actualHash = HashPassword(currentPassword, salt);

        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            throw new UnauthorizedAccessException("Current password is incorrect");

        if (newPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters");

        var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
        var newHash = HashPassword(newPassword, newSalt);

        await _store.UpdatePasswordAsync(
            Convert.ToBase64String(newHash),
            Convert.ToBase64String(newSalt),
            ct);

        _logger.LogInformation("Admin password changed");
    }

    internal static byte[] HashPassword(string password, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
    }
}
