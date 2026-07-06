using Balsm.EmergencyQr.Infrastructure.Data;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.EmergencyQr.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEmergencyQrInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection("CloudDatabase")
            .Get<DatabaseOptions>() ?? new DatabaseOptions { Provider = "postgresql", ConnectionString = string.Empty };

        services.AddDbContext<EmergencyQrDbContext>(options =>
            options.ConfigureDatabase(dbOptions, sqliteMigrationsAssembly: "Balsm.EmergencyQr.Infrastructure.Migrations.Sqlite"));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<EmergencyQrDbContext>());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
