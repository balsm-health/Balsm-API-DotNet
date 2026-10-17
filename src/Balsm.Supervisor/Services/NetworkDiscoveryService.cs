using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Balsm.Supervisor.Models;
using Microsoft.Extensions.Logging;

namespace Balsm.Supervisor.Services;

public sealed class NetworkDiscoveryService
{
    private readonly ILogger<NetworkDiscoveryService> _logger;
    private MdnsService? _mdnsService;
    private Timer? _restartDebounceTimer;

    public NetworkDiscoveryService(ILogger<NetworkDiscoveryService> logger)
    {
        _logger = logger;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
    }

    // Called by MdnsService after construction to avoid circular dependency
    public void SetMdnsService(MdnsService mdnsService)
    {
        _mdnsService = mdnsService;
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        // Debounce: wait 10 s after last change before restarting mDNS
        _restartDebounceTimer?.Dispose();
        _restartDebounceTimer = new Timer(async _ =>
        {
            if (_mdnsService is null) return;
            _logger.LogInformation("Network address changed — restarting mDNS broadcast");
            try
            {
                await _mdnsService.RestartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restart mDNS after network change");
            }
        }, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
    }

    public NetworkInfoResponse GetNetworkInfo(int port = 5050)
    {
        var addresses = GetLanAddresses();
        return new NetworkInfoResponse
        {
            Hostname = Dns.GetHostName(),
            LanAddresses = addresses.Select(a => new LanAddress
            {
                IpAddress = a.Address.ToString(),
                InterfaceName = a.Name,
                InterfaceType = a.Type,
                ApiUrl = $"http://{a.Address}:{port}",
                AdminUrl = $"http://{a.Address}:{port}/admin"
            }).ToList(),
            MdnsHostname = "balsm.local",
            MdnsApiUrl = $"http://balsm.local:{port}"
        };
    }

    public IReadOnlyList<(IPAddress Address, string Name, string Type)> GetLanAddresses()
    {
        var result = new List<(IPAddress, string, string)>();

        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

                var interfaceType = iface.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Wireless80211 => "WiFi",
                    NetworkInterfaceType.Ethernet => "Ethernet",
                    _ => iface.NetworkInterfaceType.ToString()
                };

                var props = iface.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(addr.Address)) continue;

                    result.Add((addr.Address, iface.Name, interfaceType));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate network interfaces");
        }

        return result;
    }

    public string FormatConsoleBanner(int port = 5050)
    {
        var addresses = GetLanAddresses();
        var lines = new List<string>
        {
            "",
            "╔══════════════════════════════════════════════╗",
            "║           Balsm Healthcare Platform         ║",
            "╚══════════════════════════════════════════════╝",
            "",
            "  API Endpoints:",
            $"    Local:    http://localhost:{port}",
        };

        foreach (var (addr, name, type) in addresses)
        {
            lines.Add($"    Network:  http://{addr}:{port}  ({type})");
        }

        lines.Add($"    mDNS:     http://balsm.local:{port}");
        lines.Add("");
        lines.Add("  Admin Panel:");
        lines.Add($"    Local:    http://localhost:{port}/admin");

        foreach (var (addr, _, type) in addresses)
        {
            lines.Add($"    Network:  http://{addr}:{port}/admin  ({type})");
        }

        lines.Add($"    mDNS:     http://balsm.local:{port}/admin");
        lines.Add("");

        return string.Join(Environment.NewLine, lines);
    }

    public string FormatFirstRunBanner(int port = 5050)
    {
        var httpsPort = port + 1;
        var lines = new List<string>
        {
            "",
            "╔═══════════════════════════════════════════════════╗",
            "║                                                   ║",
            "║     Balsm Healthcare Platform — First Launch      ║",
            "║                                                   ║",
            $"║  Setup URL: https://localhost:{httpsPort}/admin/setup     ║",
            "║                                                   ║",
            "║  Create your admin username and password to        ║",
            "║  secure the admin panel.                           ║",
            "║                                                   ║",
            "╚═══════════════════════════════════════════════════╝",
            ""
        };
        return string.Join(Environment.NewLine, lines);
    }

    public async Task<string?> GetPublicIpAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var ip = await http.GetStringAsync("https://api.ipify.org", ct);
            return ip.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch public IP");
            return null;
        }
    }
}
