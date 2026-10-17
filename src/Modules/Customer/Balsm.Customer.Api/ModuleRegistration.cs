using Balsm.Customer.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Customer.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddCustomerModule(this IServiceCollection services)
    {
        services.AddCustomerApplication();
        return services;
    }
}
