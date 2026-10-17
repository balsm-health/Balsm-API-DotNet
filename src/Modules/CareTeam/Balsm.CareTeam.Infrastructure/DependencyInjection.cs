using Balsm.CareTeam.Infrastructure.Data;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.CareTeam.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCareTeamInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection("CloudDatabase")
            .Get<DatabaseOptions>() ?? new DatabaseOptions { Provider = "postgresql", ConnectionString = string.Empty };

        services.AddDbContext<CareTeamDbContext>(options =>
            options.ConfigureDatabase(dbOptions, sqliteMigrationsAssembly: "Balsm.CareTeam.Infrastructure.Migrations.Sqlite"));

        // MigrationRunner discovers contexts via GetServices<DbContext>(). Without this
        // bridge the context resolves but is never migrated, so care_provider does not
        // exist in any deployed environment.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<CareTeamDbContext>());

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
