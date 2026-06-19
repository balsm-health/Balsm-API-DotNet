using Balsm.Deletion.Infrastructure.Data;
using Balsm.Deletion.Infrastructure.Jobs;
using Balsm.Deletion.Infrastructure.Services;
using Balsm.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balsm.Deletion.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDeletionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection("CloudDatabase")
            .Get<DatabaseOptions>() ?? new DatabaseOptions { Provider = "postgresql", ConnectionString = string.Empty };

        services.AddDbContext<DeletionDbContext>(options =>
            options.ConfigureDatabase(dbOptions));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<DeletionDbContext>());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        services.AddHttpClient("apple");
        services.AddScoped<AppleRevokeService>();
        services.AddHostedService<DeletionPurgeJob>();

        return services;
    }
}
