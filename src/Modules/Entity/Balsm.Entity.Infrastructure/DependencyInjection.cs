using Balsm.Infrastructure.Configuration;
using Balsm.Entity.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Entity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEntityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContext<EntityDbContext>(options =>
            options.ConfigureDatabase(dbOptions));

        return services;
    }
}
