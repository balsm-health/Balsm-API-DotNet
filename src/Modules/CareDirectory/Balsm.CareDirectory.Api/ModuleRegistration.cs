using Balsm.CareDirectory.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.CareDirectory.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddCareDirectoryModule(this IServiceCollection services)
    {
        services.AddCareDirectoryApplication();
        return services;
    }
}
