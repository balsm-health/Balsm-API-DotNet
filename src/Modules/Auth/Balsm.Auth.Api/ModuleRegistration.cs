using Balsm.Auth.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Auth.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddAuthApplication();
        return services;
    }
}
