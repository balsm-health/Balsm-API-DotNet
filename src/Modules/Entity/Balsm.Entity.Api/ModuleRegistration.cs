using Balsm.Entity.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Entity.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddEntityModule(this IServiceCollection services)
    {
        services.AddEntityApplication();
        return services;
    }
}
