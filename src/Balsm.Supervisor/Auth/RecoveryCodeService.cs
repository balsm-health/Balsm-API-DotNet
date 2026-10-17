using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace Balsm.Supervisor.Auth;

public sealed class RecoveryCodeService
{
    private const int CodeLength = 24; // bytes → 32-char base64url
    private readonly ICredentialStore _store;
    private readonly ILogger<RecoveryCodeService> _logger;

    public RecoveryCodeService(ICredentialStore store, ILogger<RecoveryCodeService> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>Generates a new recovery code, hashes it, and persists. Returns the plain-text code once.</summary>
    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var creds = await _store.LoadCredentialsAsync(ct)
            ?? throw new InvalidOperationException("No admin credentials found");

        var plainCode = Base64UrlEncode(RandomNumberGenerator.GetBytes(CodeLength));
        var codeHash = HashCode(plainCode);

        creds.RecoveryCodeHash = codeHash;
        creds.RecoveryCodeCreatedAt = DateTime.UtcNow;
        creds.RecoveryCodeUsedAt = null;
        creds.RecoveryCodeRetiredAt = null;

        await _store.SaveCredentialsAsync(creds, ct);
        _logger.LogInformation("Recovery code generated for admin {Username}", creds.Username);

        return plainCode;
    }

    /// <summary>Validates and consumes a recovery code. Returns true if valid.</summary>
    public async Task<bool> ConsumeAsync(string plainCode, CancellationToken ct = default)
    {
        var creds = await _store.LoadCredentialsAsync(ct);
        if (creds?.RecoveryCodeHash is null) return false;

        if (creds.RecoveryCodeUsedAt.HasValue || creds.RecoveryCodeRetiredAt.HasValue)
            return false;

        var expectedHash = creds.RecoveryCodeHash;
        var actualHash = HashCode(plainCode);

        var match = CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(expectedHash),
            Convert.FromBase64String(actualHash));

        if (!match) return false;

        creds.RecoveryCodeUsedAt = DateTime.UtcNow;
        await _store.SaveCredentialsAsync(creds, ct);
        _logger.LogWarning("Recovery code consumed for admin {Username}", creds.Username);
        return true;
    }

    /// <summary>Explicitly retires the current recovery code without consuming it.</summary>
    public async Task RetireAsync(CancellationToken ct = default)
    {
        var creds = await _store.LoadCredentialsAsync(ct);
        if (creds is null) return;

        creds.RecoveryCodeRetiredAt = DateTime.UtcNow;
        await _store.SaveCredentialsAsync(creds, ct);
    }

    private static string HashCode(string plainCode)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plainCode));
        return Convert.ToBase64String(hash);
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
