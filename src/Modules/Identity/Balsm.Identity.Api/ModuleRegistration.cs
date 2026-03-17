using Balsm.Identity.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Identity.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddIdentityApplication();
        return services;
    }
}
