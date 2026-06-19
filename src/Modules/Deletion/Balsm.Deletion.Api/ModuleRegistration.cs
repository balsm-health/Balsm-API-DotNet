using Balsm.Deletion.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Deletion.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddDeletionModule(this IServiceCollection services)
    {
        services.AddDeletionApplication();
        return services;
    }
}
