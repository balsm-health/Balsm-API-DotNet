using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Balsm.Entity.Application.Queries;
using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Security;
using MediatR;
using Makaretu.Dns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Balsm.Supervisor.Services;

public sealed class MdnsService : BackgroundService
{
    private readonly SupervisorOptions _options;
    private readonly NetworkDiscoveryService _networkDiscovery;
    private readonly AdminAuthService _authService;
    private readonly ServerStatusService _statusService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MdnsService> _logger;
    private MulticastService? _mdns;
    private ServiceDiscovery? _serviceDiscovery;

    public bool IsRegistered { get; private set; }
    public string? PlatformWarning { get; private set; }
    public string? MdnsName { get; private set; }

    public MdnsService(
        IOptions<SupervisorOptions> options,
        NetworkDiscoveryService networkDiscovery,
        AdminAuthService authService,
        ServerStatusService statusService,
        IServiceScopeFactory scopeFactory,
        ILogger<MdnsService> logger)
    {
        _options = options.Value;
        _networkDiscovery = networkDiscovery;
        _authService = authService;
        _statusService = statusService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        // Wire back-reference so NetworkDiscovery can trigger RestartAsync on IP change
        networkDiscovery.SetMdnsService(this);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableMdns)
        {
            _logger.LogInformation("mDNS is disabled via configuration");
            return;
        }

        CheckPlatformSupport();
        await StartMdnsAsync(stoppingToken);
    }

    public async Task RestartAsync(CancellationToken ct = default)
    {
        _serviceDiscovery?.Dispose();
        if (_mdns is not null)
        {
            _mdns.Stop();
            _mdns.Dispose();
            _mdns = null;
            _serviceDiscovery = null;
        }
        IsRegistered = false;
        await StartMdnsAsync(ct);
    }

    private async Task StartMdnsAsync(CancellationToken ct)
    {
        try
        {
            // Determine instance name per spec: balsm-setup-<short-id> before wizard, balsm-<slug> after
            var isSetup = await _authService.IsSetupCompleteAsync(ct);
            string instanceName;
            string wsName = string.Empty;
            if (!isSetup)
            {
                var shortId = (_options.ServerId ?? "local")[..Math.Min(8, (_options.ServerId ?? "local").Length)];
                instanceName = $"balsm-setup-{shortId}";
                MdnsName = instanceName;
            }
            else
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var wsResult = await mediator.Send(new GetWorkspaceQuery(), ct);
                var slug = wsResult.IsSuccess ? wsResult.Value!.Slug : "balsm";
                wsName = wsResult.IsSuccess ? wsResult.Value!.Name : string.Empty;
                instanceName = $"balsm-{slug}";
                MdnsName = instanceName;
            }

            var hostname = $"{instanceName}.local";

            // TXT record values
            var appVer = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
            var mode = _statusService.GetStatus().Mode ?? "standalone";
            var wizardFlag = isSetup ? "absent" : "required";
            var certSha256 = string.Empty;
            try
            {
                var cert = CertificateService.EnsureCertificate(_logger);
                if (cert is not null)
                    certSha256 = CertificateService.GetFingerprint(cert);
            }
            catch { /* best effort */ }

            _mdns = new MulticastService();
            _serviceDiscovery = new ServiceDiscovery(_mdns);

            // Answer A record queries for hostname
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
                        _mdns.SendAnswer(response);
                }
            };

            // Advertise with full TXT records per spec
            var serviceProfile = new ServiceProfile(
                instanceName,
                "_http._tcp",
                5050);
            serviceProfile.AddProperty("v", "1");
            serviceProfile.AddProperty("srv_id", _options.ServerId ?? string.Empty);
            serviceProfile.AddProperty("app_ver", appVer);
            serviceProfile.AddProperty("mode", mode);
            serviceProfile.AddProperty("ws_name", Uri.EscapeDataString(wsName));
            serviceProfile.AddProperty("wizard", wizardFlag);
            serviceProfile.AddProperty("http_port", "5050");
            serviceProfile.AddProperty("https_port", "5051");
            serviceProfile.AddProperty("cert_sha256", certSha256);
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
                "If balsm.local does not resolve, install Bonjour from Apple " +
                "or use the IP address shown in the Admin Panel.";
            _logger.LogInformation("{Warning}", PlatformWarning);
        }
        // macOS has Bonjour built-in — no warning needed
    }
}
