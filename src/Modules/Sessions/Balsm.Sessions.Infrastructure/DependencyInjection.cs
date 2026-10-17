using Balsm.Sessions.Infrastructure.Data;
using Balsm.Sessions.Infrastructure.Jobs;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Sessions.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSessionsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection("CloudDatabase")
            .Get<DatabaseOptions>() ?? new DatabaseOptions { Provider = "postgresql", ConnectionString = string.Empty };

        services.AddDbContext<SessionsDbContext>(options =>
            options.ConfigureDatabase(dbOptions, sqliteMigrationsAssembly: "Balsm.Sessions.Infrastructure.Migrations.Sqlite"));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<SessionsDbContext>());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddHostedService<StatusHealthJob>();

        return services;
    }
}
