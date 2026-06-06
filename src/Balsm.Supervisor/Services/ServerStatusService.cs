using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Balsm.Infrastructure.Audit;
using Balsm.Infrastructure.Platform;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Models;
using Balsm.Supervisor.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Services;

public sealed class ServerStatusService
{
    private static readonly DateTime StartedAt = DateTime.UtcNow;
    private static string? _certSha256;

    private readonly SupervisorOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ServerStatusService> _logger;

    public ServerStatusService(
        IOptions<SupervisorOptions> options,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime,
        IServiceScopeFactory scopeFactory,
        ILogger<ServerStatusService> logger)
    {
        _options = options.Value;
        _configuration = configuration;
        _lifetime = lifetime;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ApiStatusResponse GetStatus()
    {
        var process = Process.GetCurrentProcess();
        var httpPort = ReadHttpPort();

        return new ApiStatusResponse
        {
            IsRunning = true,
            Pid = process.Id,
            StartedAt = StartedAt,
            Uptime = DateTime.UtcNow - StartedAt,
            Mode = ReadCurrentMode(),
            Version = GetVersion(),
            Os = RuntimeInformation.OSDescription,
            HttpPort = httpPort,
            HttpsPort = httpPort + 1,
            DbSizeBytes = ReadDbSizeBytes(),
            CertSha256 = ReadCertFingerprint()
        };
    }

    private int ReadHttpPort()
    {
        var serverUrls = _configuration["Server:Urls"];
        if (!string.IsNullOrEmpty(serverUrls) && Uri.TryCreate(
                serverUrls.Replace("0.0.0.0", "localhost"), UriKind.Absolute, out var uri))
        {
            return uri.Port;
        }
        return 5050;
    }

    private long? ReadDbSizeBytes()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetService<PlatformDbContext>();
            var path = db?.Database.GetDbConnection().DataSource;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return new FileInfo(path).Length;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read database file size");
        }
        return null;
    }

    private string? ReadCertFingerprint()
    {
        if (_certSha256 is not null) return _certSha256;
        try
        {
            var cert = CertificateService.EnsureCertificate(_logger);
            if (cert is null) return null;
            var hash = cert.GetCertHash(HashAlgorithmName.SHA256);
            _certSha256 = string.Join(":", hash.Select(b => b.ToString("X2")));
            return _certSha256;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read certificate fingerprint");
            return null;
        }
    }

    public async Task SwitchModeAsync(string mode, int port, CancellationToken ct = default)
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");

        var urls = mode is "network" or "public"
            ? $"http://0.0.0.0:{port}"
            : $"http://localhost:{port}";

        var config = new Dictionary<string, object>
        {
            ["Server"] = new Dictionary<string, string>
            {
                ["Urls"] = urls,
                ["Mode"] = mode
            },
            ["DeploymentMode"] = "Standalone",
            ["Database"] = new Dictionary<string, string>
            {
                ["Provider"] = "Sqlite",
                ["ConnectionString"] = "Data Source=balsm.db"
            }
        };

        var json = JsonSerializer.Serialize(config,
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, json, ct);

        // Emit audit log entry for mode change
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var auditWriter = scope.ServiceProvider.GetService<IAuditLogWriter>();
            if (auditWriter is not null)
            {
                await auditWriter.WriteAsync(new AuditLog
                {
                    OccurredAt = DateTime.UtcNow,
                    Actor = "system",
                    Module = "Mode",
                    Action = "ModeChanged",
                    DetailsJson = $"{{\"mode\":\"{mode}\",\"port\":{port}}}"
                }, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write mode-change audit log");
        }

        _logger.LogInformation(
            "Mode switched to {Mode}. Signaling restart...", mode);

        // Signal the host to stop — the OS service manager will restart the process
        _lifetime.StopApplication();
    }

    public void RequestRestart()
    {
        _logger.LogInformation("Restart requested. Signaling shutdown...");
        _lifetime.StopApplication();
    }

    private string ReadCurrentMode()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json");

        if (!File.Exists(configPath)) return "local";

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Server", out var server))
            {
                // Prefer explicit Mode field
                if (server.TryGetProperty("Mode", out var modeEl))
                {
                    var mode = modeEl.GetString();
                    if (mode is "local" or "network" or "public")
                        return mode;
                }

                // Backward compat: infer from URL (0.0.0.0 → network)
                if (server.TryGetProperty("Urls", out var urls))
                {
                    var urlStr = urls.GetString() ?? "";
                    return urlStr.Contains("0.0.0.0") ? "network" : "local";
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return "local";
    }

    private static string? GetVersion()
    {
        return typeof(ServerStatusService).Assembly
            .GetName().Version?.ToString();
    }
}
