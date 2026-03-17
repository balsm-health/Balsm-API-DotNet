using Balsm.Infrastructure.Configuration;
using Balsm.SharedKernel.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        return services;
    }
}
