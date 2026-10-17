using System.Diagnostics;
using System.Runtime.InteropServices;
using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Services;

public sealed class FirstRunService : BackgroundService
{
    private readonly SupervisorOptions _options;
    private readonly ConnectionInfoService _connectionInfo;
    private readonly NetworkDiscoveryService _networkDiscovery;
    private readonly IServer _server;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FirstRunService> _logger;

    public FirstRunService(
        IOptions<SupervisorOptions> options,
        ConnectionInfoService connectionInfo,
        NetworkDiscoveryService networkDiscovery,
        IServer server,
        IServiceProvider serviceProvider,
        ILogger<FirstRunService> logger)
    {
        _options = options.Value;
        _connectionInfo = connectionInfo;
        _networkDiscovery = networkDiscovery;
        _server = server;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait briefly for Kestrel to finish binding
            await Task.Delay(2000, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var port = GetListeningPort();
        var sentinelPath = Path.GetFullPath(
            _options.FirstRunSentinelPath, AppContext.BaseDirectory);
        var isFirstRun = !File.Exists(sentinelPath);

        // Check if setup is needed
        var authService = _serviceProvider.GetRequiredService<AdminAuthService>();
        var needsSetup = !await authService.IsSetupCompleteAsync(stoppingToken);

        if (isFirstRun || needsSetup)
        {
            var banner = _networkDiscovery.FormatFirstRunBanner(port);
            _logger.LogInformation("{Banner}", banner);

            // Write sentinel
            try
            {
                await File.WriteAllTextAsync(
                    sentinelPath, DateTime.UtcNow.ToString("O"), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write first-run sentinel");
            }

            // Open browser to setup page
            var httpsPort = port + 1;
            _logger.LogInformation("Opening setup page in browser...");
            OpenBrowser($"https://localhost:{httpsPort}/admin/setup");
        }
        else
        {
            var banner = _networkDiscovery.FormatConsoleBanner(port);
            _logger.LogInformation("{Banner}", banner);
        }

        // Always refresh connection-info.txt (IPs may have changed)
        try
        {
            await _connectionInfo.WriteConnectionInfoAsync(port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write connection-info.txt");
        }
    }

    private int GetListeningPort()
    {
        try
        {
            var addresses = _server.Features.Get<IServerAddressesFeature>();
            if (addresses?.Addresses is { Count: > 0 } addrs)
            {
                foreach (var addr in addrs)
                {
                    if (Uri.TryCreate(addr, UriKind.Absolute, out var uri))
                        return uri.Port;
                }
            }
        }
        catch
        {
            // Best effort
        }

        return 5050;
    }

    private void OpenBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not open browser automatically. " +
                "Please navigate to {Url} manually.", url);
        }
    }
}
