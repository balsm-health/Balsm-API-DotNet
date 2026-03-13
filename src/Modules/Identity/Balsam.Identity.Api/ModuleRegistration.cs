using Balsam.Identity.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.Identity.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddIdentityApplication();
        return services;
    }
}
