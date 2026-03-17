using Balsm.Supervisor.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Services;

public sealed class ConnectionInfoService
{
    private readonly SupervisorOptions _options;
    private readonly NetworkDiscoveryService _networkDiscovery;
    private readonly ILogger<ConnectionInfoService> _logger;

    public ConnectionInfoService(
        IOptions<SupervisorOptions> options,
        NetworkDiscoveryService networkDiscovery,
        ILogger<ConnectionInfoService> logger)
    {
        _options = options.Value;
        _networkDiscovery = networkDiscovery;
        _logger = logger;
    }

    public async Task WriteConnectionInfoAsync(int port = 5000)
    {
        var path = Path.GetFullPath(
            _options.ConnectionInfoPath, AppContext.BaseDirectory);

        var network = _networkDiscovery.GetNetworkInfo(port);

        var lines = new List<string>
        {
            "=== Balsm Healthcare Platform — Connection Info ===",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "",
            "API Endpoints:",
            $"  Local:     http://localhost:{port}/api/v1",
            $"  mDNS:      http://balsm.local:{port}/api/v1",
        };

        foreach (var addr in network.LanAddresses)
        {
            lines.Add($"  Network:   {addr.ApiUrl}/api/v1  ({addr.InterfaceType})");
        }

        lines.Add("");
        lines.Add("Admin Panel:");
        lines.Add($"  Local:     http://localhost:{port}/admin");
        lines.Add($"  mDNS:      http://balsm.local:{port}/admin");

        foreach (var addr in network.LanAddresses)
        {
            lines.Add($"  Network:   {addr.AdminUrl}  ({addr.InterfaceType})");
        }

        lines.Add("");
        lines.Add("Health Check:");
        lines.Add($"  curl http://localhost:{port}/api/v1/health");
        lines.Add("");
        lines.Add("Admin Panel (HTTPS):");
        lines.Add($"  Local:     https://localhost:{port + 1}/admin");
        lines.Add("");
        lines.Add("Self-Signed Certificate:");
        lines.Add("  The admin panel uses a self-signed HTTPS certificate.");
        lines.Add("  Your browser will show a security warning on first visit.");
        lines.Add("  This is expected — click 'Advanced' then 'Proceed' to continue.");
        lines.Add("  On mobile: open the URL, tap 'Advanced', then 'Accept the Risk'.");
        lines.Add("");
        lines.Add("Mobile Connection:");
        lines.Add("  Open the Admin Panel from your phone using any of the Network");
        lines.Add("  URLs above. Both your phone and this computer must be on the");
        lines.Add("  same WiFi network.");
        lines.Add("");

        await File.WriteAllLinesAsync(path, lines);
        _logger.LogInformation("Connection info written to {Path}", path);
    }

    public async Task<string?> ReadConnectionInfoAsync()
    {
        var path = Path.GetFullPath(
            _options.ConnectionInfoPath, AppContext.BaseDirectory);

        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path);
    }
}
