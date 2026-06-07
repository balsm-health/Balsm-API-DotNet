using System.Security.Cryptography;
using Balsm.Supervisor.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Services;

/// <summary>
/// Generates a loopback-trust token at process startup and writes it to var/local-cli.token.
/// The CLI reads this file to authenticate read-only commands without the admin password.
/// File is mode 0400 on Unix (service user only).
/// </summary>
public sealed class LocalCliTokenService : IHostedService
{
    private static volatile string? _token;

    private readonly string _tokenPath;
    private readonly ILogger<LocalCliTokenService> _logger;

    public LocalCliTokenService(
        IOptions<SupervisorOptions> options,
        ILogger<LocalCliTokenService> logger)
    {
        var varDir = Path.Combine(AppContext.BaseDirectory, "var");
        _tokenPath = Path.GetFullPath(options.Value.LocalCliTokenPath, varDir);
        _logger = logger;
    }

    public static string? CurrentToken => _token;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dir = Path.GetDirectoryName(_tokenPath)!;
            Directory.CreateDirectory(dir);

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            File.WriteAllText(_tokenPath, token);

            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(_tokenPath, UnixFileMode.UserRead);

            _token = token;
            _logger.LogInformation("Local CLI token written to {Path}", _tokenPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write local CLI token; CLI loopback auth unavailable");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _token = null;
        try { File.Delete(_tokenPath); } catch { }
        return Task.CompletedTask;
    }
}
