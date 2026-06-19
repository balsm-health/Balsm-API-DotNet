using Balsm.Geofence.Domain;
using Balsm.Geofence.Infrastructure.Data;
using Balsm.Geofence.Infrastructure.Services;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Geofence.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGeofenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection("CloudDatabase")
            .Get<DatabaseOptions>() ?? new DatabaseOptions { Provider = "postgresql", ConnectionString = string.Empty };

        services.AddDbContext<GeofenceDbContext>(options =>
            options.ConfigureDatabase(dbOptions));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<GeofenceDbContext>());
        services.AddScoped<IGeofenceService, GeofenceService>();

        return services;
    }
}
