using Balsm.Supervisor.Auth;
using Balsm.Supervisor.Configuration;
using Balsm.Supervisor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Supervisor;

public static class SupervisorRegistration
{
    public static IServiceCollection AddSupervisorModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SupervisorOptions>(
            configuration.GetSection(SupervisorOptions.SectionName));

        // Auth services
        services.AddSingleton<ICredentialStore, FileCredentialStore>();
        services.AddSingleton<AdminAuthService>();
        services.AddSingleton<AdminSessionService>();

        // Core services
        services.AddSingleton<NetworkDiscoveryService>();
        services.AddSingleton<ConnectionInfoService>();
        services.AddSingleton<ServerStatusService>();
        services.AddSingleton<SelfUpdateService>();

        // Register as singletons first so controllers can inject them,
        // then wire them up as hosted services
        services.AddSingleton<MdnsService>();
        services.AddHostedService(sp => sp.GetRequiredService<MdnsService>());
        services.AddSingleton<CloudflareTunnelService>();
        services.AddHostedService(sp => sp.GetRequiredService<CloudflareTunnelService>());
        services.AddSingleton<FirstRunService>();
        services.AddHostedService(sp => sp.GetRequiredService<FirstRunService>());

        // Federation services
        services.AddSingleton<IFederationStore, FileFederationStore>();
        services.AddSingleton<FederationService>();
        services.AddSingleton<SyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<SyncService>());

        services.AddHttpClient("GitHub", client =>
        {
            client.BaseAddress = new Uri("https://api.github.com");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            client.DefaultRequestHeaders.Add("User-Agent", "Balsm-Updater");
        });

        services.AddHttpClient("BalsmRegistry", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Balsm-Supervisor");
        });

        services.AddHttpClient("Federation", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Balsm-Federation");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
