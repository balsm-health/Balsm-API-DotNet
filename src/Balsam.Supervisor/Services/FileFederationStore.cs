using System.Text.Json;
using Balsam.Supervisor.Configuration;
using Balsam.Supervisor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsam.Supervisor.Services;

public sealed class FileFederationStore : IFederationStore
{
    private readonly string _filePath;
    private readonly ILogger<FileFederationStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FileFederationStore(
        IOptions<SupervisorOptions> options,
        ILogger<FileFederationStore> logger)
    {
        _filePath = Path.GetFullPath(
            options.Value.FederationDataPath, AppContext.BaseDirectory);
        _logger = logger;
    }

    public async Task<FederationData> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath)) return new FederationData();

        await _lock.WaitAsync(ct);
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, ct);
            return JsonSerializer.Deserialize<FederationData>(json, JsonOptions)
                   ?? new FederationData();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(FederationData data, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);
            SetFilePermissions();
            _logger.LogInformation("Federation data saved to {Path}", _filePath);
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
