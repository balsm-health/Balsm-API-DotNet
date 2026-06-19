using Balsm.Disclosure.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Disclosure.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddDisclosureModule(this IServiceCollection services)
    {
        services.AddDisclosureApplication();
        return services;
    }
}
