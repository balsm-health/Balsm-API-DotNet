using Balsam.Customer.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsam.Customer.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddCustomerModule(this IServiceCollection services)
    {
        services.AddCustomerApplication();
        return services;
    }
}
