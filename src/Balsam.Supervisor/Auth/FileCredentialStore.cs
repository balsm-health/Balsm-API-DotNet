using System.Text.Json;
using Balsam.Supervisor.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsam.Supervisor.Auth;

public sealed class FileCredentialStore : ICredentialStore
{
    private readonly string _filePath;
    private readonly ILogger<FileCredentialStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FileCredentialStore(
        IOptions<SupervisorOptions> options,
        ILogger<FileCredentialStore> logger)
    {
        _filePath = Path.GetFullPath(
            options.Value.CredentialsPath, AppContext.BaseDirectory);
        _logger = logger;
    }

    public Task<bool> HasCredentialsAsync(CancellationToken ct = default)
        => Task.FromResult(File.Exists(_filePath));

    public async Task SaveCredentialsAsync(AdminCredentials credentials, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(credentials, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);
            SetFilePermissions();
            _logger.LogInformation("Admin credentials saved to {Path}", _filePath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AdminCredentials?> LoadCredentialsAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath)) return null;

        await _lock.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            return JsonSerializer.Deserialize<AdminCredentials>(json, JsonOptions);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdatePasswordAsync(string passwordHash, string salt, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!File.Exists(_filePath))
                throw new InvalidOperationException("No credentials to update");

            var json = await File.ReadAllTextAsync(_filePath, ct);
            var creds = JsonSerializer.Deserialize<AdminCredentials>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to read credentials");

            creds.PasswordHash = passwordHash;
            creds.Salt = salt;
            creds.LastPasswordChange = DateTime.UtcNow;

            var updatedJson = JsonSerializer.Serialize(creds, JsonOptions);
            await File.WriteAllTextAsync(_filePath, updatedJson, ct);
            SetFilePermissions();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void SetFilePermissions()
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
