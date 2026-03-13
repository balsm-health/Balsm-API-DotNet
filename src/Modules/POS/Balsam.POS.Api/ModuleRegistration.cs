using Balsam.POS.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.POS.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddPOSModule(this IServiceCollection services)
    {
        services.AddPOSApplication();
        return services;
    }
}
