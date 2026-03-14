using Balsam.Supervisor.Auth;
using Balsam.Supervisor.Configuration;
using Balsam.Supervisor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.Supervisor;

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

        services.AddHttpClient("GitHub", client =>
        {
            client.BaseAddress = new Uri("https://api.github.com");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            client.DefaultRequestHeaders.Add("User-Agent", "Balsam-Updater");
        });

        return services;
    }
}
