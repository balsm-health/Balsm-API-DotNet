using Balsm.CareTeam.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.CareTeam.Api;

public static class ModuleRegistration
{
    public static IServiceCollection AddCareTeamModule(this IServiceCollection services)
    {
        services.AddCareTeamApplication();
        return services;
    }
}
