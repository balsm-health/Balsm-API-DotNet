using Microsoft.Extensions.DependencyInjection;

namespace Balsm.CareTeam.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCareTeamApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}
