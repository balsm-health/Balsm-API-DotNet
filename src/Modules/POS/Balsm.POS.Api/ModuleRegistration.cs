using Balsm.POS.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.POS.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddPOSModule(this IServiceCollection services)
    {
        services.AddPOSApplication();
        return services;
    }
}
