using Balsm.Account.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Account.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddAccountModule(this IServiceCollection services)
    {
        services.AddAccountApplication();
        return services;
    }
}
