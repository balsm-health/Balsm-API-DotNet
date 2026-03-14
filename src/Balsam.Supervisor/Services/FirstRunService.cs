using System.Diagnostics;
using System.Runtime.InteropServices;
using Balsam.Supervisor.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsam.Supervisor.Services;

public sealed class FirstRunService : BackgroundService
{
    private readonly SupervisorOptions _options;
    private readonly ConnectionInfoService _connectionInfo;
    private readonly NetworkDiscoveryService _networkDiscovery;
    private readonly IServer _server;
    private readonly ILogger<FirstRunService> _logger;

    public FirstRunService(
        IOptions<SupervisorOptions> options,
        ConnectionInfoService connectionInfo,
        NetworkDiscoveryService networkDiscovery,
        IServer server,
        ILogger<FirstRunService> logger)
    {
        _options = options.Value;
        _connectionInfo = connectionInfo;
        _networkDiscovery = networkDiscovery;
        _server = server;
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

        // Detect actual listening port from the server
        var port = GetListeningPort();

        var banner = _networkDiscovery.FormatConsoleBanner(port);
        _logger.LogInformation("{Banner}", banner);

        try
        {
            await _connectionInfo.WriteConnectionInfoAsync(port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write connection-info.txt");
        }

        // Open admin panel in browser on every start
        _logger.LogInformation("Opening admin panel in browser...");
        OpenBrowser($"http://localhost:{port}/admin");
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
