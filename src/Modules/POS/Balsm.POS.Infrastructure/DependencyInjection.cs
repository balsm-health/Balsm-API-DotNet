using Balsm.Infrastructure.Configuration;
using Balsm.POS.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.POS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPOSInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services.AddDbContext<POSDbContext>(options =>
            options.ConfigureDatabase(dbOptions));

        return services;
    }
}
