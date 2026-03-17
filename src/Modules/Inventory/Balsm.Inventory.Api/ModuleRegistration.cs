using Balsm.Inventory.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Inventory.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddInventoryApplication();
        return services;
    }
}
