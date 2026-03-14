using System.Diagnostics;
using System.Text.RegularExpressions;
using Balsam.Supervisor.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsam.Supervisor.Services;

public sealed partial class CloudflareTunnelService : BackgroundService
{
    private readonly SupervisorOptions _options;
    private readonly ILogger<CloudflareTunnelService> _logger;
    private Process? _process;
    private readonly object _lock = new();

    public bool IsRunning { get; private set; }
    public string? TunnelUrl { get; private set; }
    public string? TunnelType { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool IsCloudflaredInstalled { get; private set; }
    public string? RegisteredUrl { get; set; }

    public CloudflareTunnelService(
        IOptions<SupervisorOptions> options,
        ILogger<CloudflareTunnelService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IsCloudflaredInstalled = CheckCloudflaredInstalled();
        RegisteredUrl = _options.TunnelUrl;

        if (!IsCloudflaredInstalled)
        {
            _logger.LogInformation("cloudflared not found. Tunnel features disabled.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("cloudflared detected. Tunnel features available.");

        // If a token is configured, auto-start a named tunnel
        if (_options.EnableTunnel && !string.IsNullOrEmpty(_options.TunnelToken))
        {
            _ = StartTunnelAsync("named", _options.TunnelToken, 5050, stoppingToken);
        }

        return Task.CompletedTask;
    }

    public async Task StartTunnelAsync(string type, string? token, int port, CancellationToken ct = default)
    {
        if (IsRunning)
        {
            await StopTunnelAsync();
        }

        if (!IsCloudflaredInstalled)
        {
            ErrorMessage = "cloudflared is not installed.";
            return;
        }

        ErrorMessage = null;
        TunnelType = type;

        var cloudflaredPath = _options.CloudflaredPath ?? "cloudflared";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cloudflaredPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (type == "named" && !string.IsNullOrEmpty(token))
            {
                psi.ArgumentList.Add("tunnel");
                psi.ArgumentList.Add("run");
                psi.ArgumentList.Add("--token");
                psi.ArgumentList.Add(token);
            }
            else
            {
                // Quick tunnel
                psi.ArgumentList.Add("tunnel");
                psi.ArgumentList.Add("--url");
                psi.ArgumentList.Add($"http://localhost:{port}");
            }

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.ErrorDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                _logger.LogDebug("cloudflared: {Line}", e.Data);

                // Quick tunnel URL appears in stderr
                if (TunnelUrl is null)
                {
                    var match = TunnelUrlRegex().Match(e.Data);
                    if (match.Success)
                    {
                        TunnelUrl = match.Value;
                        _logger.LogInformation("Tunnel URL: {Url}", TunnelUrl);
                    }
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Data)) return;
                _logger.LogDebug("cloudflared: {Line}", e.Data);
            };

            process.Exited += (_, _) =>
            {
                _logger.LogInformation("cloudflared process exited");
                lock (_lock)
                {
                    IsRunning = false;
                    TunnelUrl = null;
                }
            };

            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            lock (_lock)
            {
                _process = process;
                IsRunning = true;
                // For named tunnels, use the registered URL immediately
                if (type == "named" && RegisteredUrl is not null)
                {
                    TunnelUrl = RegisteredUrl;
                }
            }

            _logger.LogInformation("Started cloudflared tunnel (type={Type}, port={Port})", type, port);

            // For quick tunnels, wait briefly for URL to appear
            if (type != "named")
            {
                for (var i = 0; i < 15 && TunnelUrl is null && !process.HasExited; i++)
                {
                    await Task.Delay(1000, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start cloudflared tunnel");
            ErrorMessage = $"Failed to start tunnel: {ex.Message}";
            IsRunning = false;
        }
    }

    public Task StopTunnelAsync()
    {
        lock (_lock)
        {
            if (_process is { HasExited: false } proc)
            {
                try
                {
                    proc.Kill(entireProcessTree: true);
                    proc.Dispose();
                    _logger.LogInformation("Stopped cloudflared tunnel");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping cloudflared process");
                }
            }

            _process = null;
            IsRunning = false;
            TunnelUrl = null;
            TunnelType = null;
            ErrorMessage = null;
        }

        return Task.CompletedTask;
    }

    private bool CheckCloudflaredInstalled()
    {
        var path = _options.CloudflaredPath ?? "cloudflared";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await StopTunnelAsync();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        lock (_lock)
        {
            if (_process is { HasExited: false } proc)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
                proc.Dispose();
            }
            _process = null;
        }
        base.Dispose();
    }

    [GeneratedRegex(@"https://[a-zA-Z0-9\-]+\.trycloudflare\.com")]
    private static partial Regex TunnelUrlRegex();
}
