using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Balsm.Supervisor.Auth;

public sealed class AdminAuthService
{
    private const int SaltSize = 16;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(1);

    private readonly ICredentialStore _store;
    private readonly ILogger<AdminAuthService> _logger;
    private readonly IReadOnlyDictionary<string, IPasswordHasher> _hashers;
    private readonly IPasswordHasher _preferred;

    public AdminAuthService(
        ICredentialStore store,
        IEnumerable<IPasswordHasher> hashers,
        ILogger<AdminAuthService> logger)
    {
        _store = store;
        _logger = logger;
        _hashers = hashers.ToDictionary(h => h.AlgorithmId, StringComparer.OrdinalIgnoreCase);
        _preferred = _hashers.TryGetValue("argon2id", out var a) ? a : _hashers.Values.First();
    }

    public async Task<bool> IsSetupCompleteAsync(CancellationToken ct = default)
        => await _store.HasCredentialsAsync(ct);

    public async Task SetupAsync(string username, string password, string locale = "en", CancellationToken ct = default)
    {
        if (await _store.HasCredentialsAsync(ct))
            throw new InvalidOperationException("Setup already completed");

        if (password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = _preferred.Hash(password, salt);

        var creds = new AdminCredentials
        {
            Username = username,
            PasswordHash = Convert.ToBase64String(hash),
            Salt = Convert.ToBase64String(salt),
            PasswordHashAlgorithm = _preferred.AlgorithmId,
            CreatedAt = DateTime.UtcNow,
            Locale = locale,
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
        var storedHash = Convert.FromBase64String(creds.PasswordHash);

        var algorithmId = creds.PasswordHashAlgorithm ?? "pbkdf2";
        if (!_hashers.TryGetValue(algorithmId, out var hasher))
        {
            _logger.LogError("Unknown password hash algorithm: {Algorithm}", algorithmId);
            return LoginResult.Failure("Internal error");
        }

        var passwordMatch = hasher.Verify(password, salt, storedHash);
        var usernameMatch = string.Equals(username, creds.Username, StringComparison.Ordinal);

        if (!passwordMatch || !usernameMatch)
        {
            creds.FailedLoginAttempts++;

            if (creds.FailedLoginAttempts >= MaxFailedAttempts)
            {
                creds.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
                _logger.LogWarning("Admin account locked out until {LockoutEnd}", creds.LockoutEnd);
            }

            await _store.SaveCredentialsAsync(creds, ct);
            await Task.Delay(FailureDelay, ct);

            return creds.FailedLoginAttempts >= MaxFailedAttempts
                ? LoginResult.LockedOut(LockoutDuration)
                : LoginResult.Failure("Invalid credentials");
        }

        // Upgrade hash algorithm if not using preferred
        if (!string.Equals(algorithmId, _preferred.AlgorithmId, StringComparison.OrdinalIgnoreCase))
        {
            var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
            var newHash = _preferred.Hash(password, newSalt);
            creds.PasswordHash = Convert.ToBase64String(newHash);
            creds.Salt = Convert.ToBase64String(newSalt);
            creds.PasswordHashAlgorithm = _preferred.AlgorithmId;
            _logger.LogInformation("Upgraded password hash algorithm to {Alg}", _preferred.AlgorithmId);
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
        var storedHash = Convert.FromBase64String(creds.PasswordHash);
        var algorithmId = creds.PasswordHashAlgorithm ?? "pbkdf2";

        if (!_hashers.TryGetValue(algorithmId, out var hasher) ||
            !hasher.Verify(currentPassword, salt, storedHash))
            throw new UnauthorizedAccessException("Current password is incorrect");

        if (newPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters");

        var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
        var newHash = _preferred.Hash(newPassword, newSalt);

        creds.PasswordHash = Convert.ToBase64String(newHash);
        creds.Salt = Convert.ToBase64String(newSalt);
        creds.PasswordHashAlgorithm = _preferred.AlgorithmId;
        creds.LastPasswordChange = DateTime.UtcNow;

        await _store.SaveCredentialsAsync(creds, ct);
        _logger.LogInformation("Admin password changed");
    }

    public async Task<string> GetLocaleAsync(CancellationToken ct = default)
    {
        var creds = await _store.LoadCredentialsAsync(ct);
        return creds?.Locale ?? "en";
    }

    public async Task SetLocaleAsync(string locale, CancellationToken ct = default)
        => await _store.UpdateLocaleAsync(locale, ct);

    /// <summary>Used by recovery flow — bypasses current-password check.</summary>
    internal async Task ChangePasswordAsync_Internal(string newPassword, CancellationToken ct = default)
    {
        var creds = await _store.LoadCredentialsAsync(ct)
            ?? throw new InvalidOperationException("No credentials configured");

        if (newPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters");

        var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
        var newHash = _preferred.Hash(newPassword, newSalt);

        creds.PasswordHash = Convert.ToBase64String(newHash);
        creds.Salt = Convert.ToBase64String(newSalt);
        creds.PasswordHashAlgorithm = _preferred.AlgorithmId;
        creds.LastPasswordChange = DateTime.UtcNow;

        await _store.SaveCredentialsAsync(creds, ct);
        _logger.LogInformation("Admin password reset via recovery code");
    }
}
