using Balsm.Sessions.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Sessions.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddSessionsModule(this IServiceCollection services)
    {
        services.AddSessionsApplication();
        return services;
    }
}
