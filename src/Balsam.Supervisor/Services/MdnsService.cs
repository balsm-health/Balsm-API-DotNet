using System.Net;
using System.Runtime.InteropServices;
using Balsam.Supervisor.Configuration;
using Makaretu.Dns;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsam.Supervisor.Services;

public sealed class MdnsService : BackgroundService
{
    private readonly SupervisorOptions _options;
    private readonly NetworkDiscoveryService _networkDiscovery;
    private readonly ILogger<MdnsService> _logger;
    private MulticastService? _mdns;
    private ServiceDiscovery? _serviceDiscovery;

    public bool IsRegistered { get; private set; }
    public string? PlatformWarning { get; private set; }

    public MdnsService(
        IOptions<SupervisorOptions> options,
        NetworkDiscoveryService networkDiscovery,
        ILogger<MdnsService> logger)
    {
        _options = options.Value;
        _networkDiscovery = networkDiscovery;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableMdns)
        {
            _logger.LogInformation("mDNS is disabled via configuration");
            return Task.CompletedTask;
        }

        CheckPlatformSupport();

        try
        {
            _mdns = new MulticastService();
            _serviceDiscovery = new ServiceDiscovery(_mdns);

            var hostname = $"{_options.MdnsHostname}.local";

            // Respond to DNS queries for "balsam.local" with this machine's LAN IPs.
            // ServiceDiscovery.Advertise() only registers a DNS-SD service record (_http._tcp)
            // — it does NOT create the A record that makes "balsam.local" actually resolve.
            _mdns.QueryReceived += (sender, e) =>
            {
                var msg = e.Message;
                foreach (var question in msg.Questions)
                {
                    if (!question.Name.ToString().Equals(hostname, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (question.Type is not (DnsType.A or DnsType.ANY))
                        continue;

                    var response = msg.CreateResponse();
                    var currentAddresses = _networkDiscovery.GetLanAddresses();
                    foreach (var (addr, _, _) in currentAddresses)
                    {
                        if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                            continue;

                        response.Answers.Add(new ARecord
                        {
                            Name = hostname,
                            Address = addr,
                            TTL = TimeSpan.FromMinutes(2)
                        });
                    }

                    if (response.Answers.Count > 0)
                    {
                        _mdns.SendAnswer(response);
                    }
                }
            };

            // Also advertise as a discoverable HTTP service (for DNS-SD / Bonjour browsers)
            var serviceProfile = new ServiceProfile(
                _options.MdnsHostname,
                "_http._tcp",
                5050);
            serviceProfile.AddProperty("path", "/");
            serviceProfile.AddProperty("product", "Balsam Healthcare Platform");
            _serviceDiscovery.Advertise(serviceProfile);

            _mdns.Start();

            var lanAddresses = _networkDiscovery.GetLanAddresses();
            IsRegistered = true;
            _logger.LogInformation(
                "mDNS active: {Hostname} resolving to {Count} address(es)",
                hostname, lanAddresses.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to start mDNS. The admin panel is still accessible via IP address.");
            IsRegistered = false;
        }

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _serviceDiscovery?.Dispose();
        if (_mdns is not null)
        {
            _mdns.Stop();
            _mdns.Dispose();
        }
        base.Dispose();
    }

    private void CheckPlatformSupport()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                var avahiPath = "/usr/sbin/avahi-daemon";
                if (!File.Exists(avahiPath))
                {
                    PlatformWarning = "mDNS requires avahi-daemon on Linux. " +
                        "Install it with: sudo apt install avahi-daemon";
                    _logger.LogWarning("{Warning}", PlatformWarning);
                }
            }
            catch
            {
                // Best effort check
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            PlatformWarning = "mDNS on Windows may require Bonjour Print Services. " +
                "If balsam.local does not resolve, install Bonjour from Apple " +
                "or use the IP address shown in the Admin Panel.";
            _logger.LogInformation("{Warning}", PlatformWarning);
        }
        // macOS has Bonjour built-in — no warning needed
    }
}
