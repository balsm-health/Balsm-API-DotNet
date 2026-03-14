using System.Diagnostics;
using System.Text.Json;
using Balsam.Supervisor.Configuration;
using Balsam.Supervisor.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsam.Supervisor.Services;

public sealed class ServerStatusService
{
    private static readonly DateTime StartedAt = DateTime.UtcNow;

    private readonly SupervisorOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ServerStatusService> _logger;

    public ServerStatusService(
        IOptions<SupervisorOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<ServerStatusService> logger)
    {
        _options = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    public ApiStatusResponse GetStatus()
    {
        var process = Process.GetCurrentProcess();

        return new ApiStatusResponse
        {
            IsRunning = true,
            Pid = process.Id,
            StartedAt = StartedAt,
            Uptime = DateTime.UtcNow - StartedAt,
            Mode = ReadCurrentMode(),
            Version = GetVersion()
        };
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
                ["ConnectionString"] = "Data Source=balsam.db"
            }
        };

        var json = JsonSerializer.Serialize(config,
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, json, ct);

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
