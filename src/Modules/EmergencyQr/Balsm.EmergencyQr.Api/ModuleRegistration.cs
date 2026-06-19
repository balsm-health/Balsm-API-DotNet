using Balsm.EmergencyQr.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.EmergencyQr.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddEmergencyQrModule(this IServiceCollection services)
    {
        services.AddEmergencyQrApplication();
        return services;
    }
}
